import * as vscode from 'vscode';
import * as child_process from 'child_process';
import * as path from 'path';

// ---------------------------------------------------------------------------
// Activation
// ---------------------------------------------------------------------------

let diagnosticCollection: vscode.DiagnosticCollection;

/**
 * Activate the extension.
 */
export function activate(context: vscode.ExtensionContext): void {
    // ---- formatter --------------------------------------------------------
    const formatter = vscode.languages.registerDocumentFormattingEditProvider(
        'blacksmith-dsl-editor',
        {
            provideDocumentFormattingEdits(
                document: vscode.TextDocument,
                options: vscode.FormattingOptions
            ): vscode.TextEdit[] {
                const text = document.getText();
                const formatted = formatBlacksmithDSL(text, options);

                const fullRange = new vscode.Range(
                    document.positionAt(0),
                    document.positionAt(text.length)
                );

                return [vscode.TextEdit.replace(fullRange, formatted)];
            }
        }
    );

    context.subscriptions.push(formatter);

    // Also register as a range formatter so "Format Selection" works.
    const rangeFormatter = vscode.languages.registerDocumentRangeFormattingEditProvider(
        'blacksmith-dsl-editor',
        {
            provideDocumentRangeFormattingEdits(
                document: vscode.TextDocument,
                range: vscode.Range,
                options: vscode.FormattingOptions
            ): vscode.TextEdit[] {
                const text = document.getText(range);
                const formatted = formatBlacksmithDSL(text, options);

                return [vscode.TextEdit.replace(range, formatted)];
            }
        }
    );

    context.subscriptions.push(rangeFormatter);

    // ---- C# validator integration -----------------------------------------
    diagnosticCollection = vscode.languages.createDiagnosticCollection('blacksmith-dsl');
    context.subscriptions.push(diagnosticCollection);

    // Path to the bundled C# validator executable
    const validatorPath = path.join(context.extensionPath, 'bin', 'bdsl-validator.exe');

    // Validate on open, save, and change
    context.subscriptions.push(
        vscode.workspace.onDidOpenTextDocument(doc => validateDocument(doc, validatorPath))
    );
    context.subscriptions.push(
        vscode.workspace.onDidSaveTextDocument(doc => validateDocument(doc, validatorPath))
    );
    // Throttled re-validate on change (every 500 ms)
    const changeTimers = new Map<string, NodeJS.Timeout>();
    context.subscriptions.push(
        vscode.workspace.onDidChangeTextDocument(e => {
            if (e.document.languageId !== 'blacksmith-dsl-editor') return;
            const uri = e.document.uri.toString();
            if (changeTimers.has(uri)) clearTimeout(changeTimers.get(uri));
            changeTimers.set(uri, setTimeout(() => {
                changeTimers.delete(uri);
                validateDocument(e.document, validatorPath);
            }, 500));
        })
    );

    // Validate already-open documents
    vscode.workspace.textDocuments.forEach(doc => validateDocument(doc, validatorPath));
}

// ---------------------------------------------------------------------------
// C# Validator (subprocess)
// ---------------------------------------------------------------------------

/**
 * Run the C# validator as a child process, feeding the document text via
 * stdin, and update the diagnostic collection with the result.
 */
function validateDocument(document: vscode.TextDocument, validatorPath: string): void {
    if (document.languageId !== 'blacksmith-dsl-editor') return;

    const child = child_process.spawn(validatorPath, [], {
        stdio: ['pipe', 'pipe', 'pipe'] as const,
        windowsHide: true
    });

    let stdout = '';
    let stderr = '';

    child.stdout.on('data', (data: Buffer) => { stdout += data.toString(); });
    child.stderr.on('data', (data: Buffer) => { stderr += data.toString(); });

    child.on('close', (code: number | null) => {
        if (code !== 0 || stderr.length > 0) {
            // Validator crashed — clear diagnostics, don't annoy the user
            diagnosticCollection.delete(document.uri);
            return;
        }

        const diagnostics: vscode.Diagnostic[] = [];
        try {
            const result = JSON.parse(stdout) as {
                hasError?: boolean;
                errorSpans?: ErrorSpan[];
                message?: string;
            };
            if (result.hasError) {
                const spans = result.errorSpans || [];
                const msg = result.message || 'Syntax error';

                for (const span of spans) {
                    const line = Math.max(0, (span.line || 1) - 1);       // 1-based → 0-based
                    const startCol = Math.max(0, (span.startColumn || 1) - 1);
                    const endCol = Math.max(startCol + 1, span.endColumn || span.startColumn || 1);
                    // C# endColumn is 1-based inclusive — VS Code expects exclusive,
                    // so inclusive endColumn means "last char position", exclusive is endColumn.
                    const range = new vscode.Range(line, startCol, line, endCol);
                    const diag = new vscode.Diagnostic(
                        range,
                        msg,
                        vscode.DiagnosticSeverity.Error
                    );
                    diag.source = 'bdsl-validator';
                    diagnostics.push(diag);
                }
            }
        } catch {
            // JSON parse failed — ignore
        }

        diagnosticCollection.set(document.uri, diagnostics);
    });

    child.on('error', () => {
        diagnosticCollection.delete(document.uri);
    });

    // Feed the document text to stdin and close it
    child.stdin.write(document.getText());
    child.stdin.end();
}

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface ErrorSpan {
    line?: number;
    startColumn?: number;
    endColumn?: number;
}

interface CommentRemovalResult {
    cleanLine: string;
    stillInBlockComment: boolean;
}

// ---------------------------------------------------------------------------
// Formatter
// ---------------------------------------------------------------------------

/**
 * Format Blacksmith DSL text with C++-style indentation.
 *
 * Rules:
 *  - Trailing whitespace is stripped.
 *  - Leading whitespace is replaced by brace-depth indentation.
 *  - Line comments (//) and block comments (slash-star ... star-slash) are ignored for
 *    brace counting so braces inside comments don't affect indentation.
 *  - Empty lines are preserved without trailing whitespace.
 */
function formatBlacksmithDSL(text: string, options: vscode.FormattingOptions): string {
    const indentChar = options.insertSpaces ? ' ' : '\t';
    const indentSize = options.tabSize;

    // ---- step 1: split & trim trailing whitespace ----------------------
    let lines = text.split(/\r?\n/).map(l => l.trimEnd());

    // ---- step 2: collapse empty bracket pairs across lines FIRST -------
    //  "{\n}" → "{}"   "(\n)" → "()"   "[\n]" → "[]"
    lines = standardizeLineBreaks(lines);
    lines = collapseEmptyBrackets(lines);

    // ---- step 3: simple indent — close before, open after -------------
    // Same-line ( ) and { } pairs cancel out via a stack, so only
    // *unmatched* brackets affect indentation.  Everything else is the
    // simplest possible rule: close → outdent before, open → indent after.
    const formattedLines: string[] = [];
    let indentLevel = 0;
    let inBlockComment = false;
    let nextClose = 0;
    for (const rawLine of lines) {
        if (rawLine.length === 0) {
            formattedLines.push('');
            continue;
        }

        const { cleanLine, stillInBlockComment } = removeComments(rawLine, inBlockComment);
        inBlockComment = stillInBlockComment;

        // Same-line pair cancellation via stack
        const stack: string[] = [];
        let closeCount = 0;
        for (const ch of cleanLine) {
            if (ch === '(' || ch === '{') {
                stack.push(ch);
            } else if (ch === ')') {
                if (stack.length > 0 && stack[stack.length - 1] === '(') {
                    stack.pop();          // paired → cancel
                } else {
                    closeCount++;         // unmatched close
                }
            } else if (ch === '}') {
                if (stack.length > 0 && stack[stack.length - 1] === '{') {
                    stack.pop();          // paired → cancel
                } else {
                    closeCount++;         // unmatched close
                }
            }
        }
        const openCount = stack.length;   // unmatched opens

        if(cleanLine.trim() === '}'){
            nextClose = closeCount;
        }
        indentLevel = Math.max(0, indentLevel - nextClose);
        nextClose = closeCount;

        if(cleanLine.trim() === '}'){
            nextClose = 0;
        }
        

        const indentation = indentChar.repeat(indentLevel * indentSize);
        formattedLines.push(indentation + rawLine.trimStart());

        indentLevel += openCount;
    }

    // ---- step 4: per-line micro-formatting -----------------------------
    return postProcessPerLine(formattedLines).join('\n');
}

// 合并跨行的但中间无内容，或者只有一段连续字符的括号和字符串
function collapseEmptyBrackets(lines: string[]): string[] {
    const result: string[] = [];
    let i = 0;
    while (i < lines.length) {
        const line = lines[i];
        if(line.endsWith('(') || /\([^)]+$/.test(line)){
            while(i + 1 < lines.length && lines[i + 1].trim().length == 0){
                i++;
            }
            if(i + 1 < lines.length){
                const next =  lines[i + 1].trimStart();
                if(next.startsWith(')') || /^[^(]+\)/.test(next)){
                    result.push(line + lines[i + 1].trimStart());
                    i += 2;
                    continue;
                }
            }
        }
        if(line.endsWith('[') || /\[[^\]]+$/.test(line)){
            while(i + 1 < lines.length && lines[i + 1].trim().length == 0){
                i++;
            }
            if(i + 1 < lines.length){
                const next =  lines[i + 1].trimStart();
                if(next.startsWith(']') || /^[^\[]+\]/.test(next)){
                    result.push(line + lines[i + 1].trimStart());
                    i += 2;
                    continue;
                }
            }
        }
        if(line.endsWith('{') || /\{[^}]+$/.test(line)){
            while(i + 1 < lines.length && lines[i + 1].trim().length == 0){
                i++;
            }
            if(i + 1 < lines.length){
                const next =  lines[i + 1].trimStart();
                if(next.startsWith('}') || /^[^{]+\}/.test(next)){
                    result.push(line + lines[i + 1].trimStart());
                    i += 2;
                    continue;
                }
            }
        }
        result.push(line);
        i++;
    }
    return result;
}

function standardizeLineBreaks(lines: string[]): string[] {
    const temp: string[] = [];
    for(let i = 0; i < lines.length; i++){
        if(lines[i].trim() === ''){
            continue;
        }
        temp.push(...splitBeforeBracket(lines[i]))
    }
    const res: string[] = [];
    let i = 0;
    while (i < temp.length) {
        let originI = i;
        while(i + 1 < temp.length && /^(\(|->|\{)/.test(temp[i + 1].trim())){
            i++;
        }
        res.push(temp.slice(originI, i + 1).join(''));
        i++;
    }
    return res;
}

function splitBeforeBracket(str: string): string[] {
    if (!str) return [];
    
    str = str.trim();
    const result: string[] = [];
    let lastIndex = 0;
    
    for (let i = 0; i < str.length; i++) {
        if (str[i] === '[' || str[i] === '<' || str[i] === '}' || str[i] === ')') {
            // 如果当前位置是 '['，且不是第一个字符
            if (i > lastIndex) {
                result.push(str.substring(lastIndex, i).trim());
            }
            // 更新 lastIndex 到当前 '[' 位置
            lastIndex = i;
        }
    }
    
    // 处理剩余部分
    if (lastIndex < str.length) {
        result.push(str.substring(lastIndex).trim());
    }
    
    return result;
}

// 去除括号内多余的空格
function postProcessPerLine(lines: string[]): string[] {
    return lines.map(line => {
        // Remove spaces around ->
        let fixed = line.replace(/\s*->\s*/g, '->');

        // Tight brackets: strip spaces between bracket and content.
        //  "( foo )" → "(foo)"   "{ bar }" → "{bar}"
        //  "[ baz ]" → "[baz]"   "< qux >" → "<qux>"
        //
        // Opening side: \s+ after the bracket is safe to strip.
        //  <\s+(?!=)  avoids eating the space in "<= " (operator).
        fixed = fixed.replace(/\(\s+/g, '(');
        fixed = fixed.replace(/\{\s+/g, '{');
        fixed = fixed.replace(/\[\s+/g, '[');
        fixed = fixed.replace(/<\s+(?!=)/g, '<');

        // Closing side: only strip spaces that follow real content (not
        // leading indentation).  (\S) captures the last content char so
        // we never eat the indentation before a standalone ")" or "}".
        //  \s+>(?!=)  avoids eating the space before ">=" (operator).
        fixed = fixed.replace(/(\S)\s+\)/g, '$1)');
        fixed = fixed.replace(/(\S)\s+\}/g, '$1}');
        fixed = fixed.replace(/(\S)\s+\]/g, '$1]');
        fixed = fixed.replace(/(\S)\s+>(?!=)/g, '$1>');

        return fixed;
    });
}

/**
 * Strip comments from a line, respecting block-comment state.
 */
function removeComments(line: string, inBlockComment: boolean): CommentRemovalResult {
    if (inBlockComment) {
        const endIdx = line.indexOf('*/');
        if (endIdx === -1) {
            // Entire line is inside a block comment — no braces to count.
            return { cleanLine: '', stillInBlockComment: true };
        }
        // Block comment ends on this line; process the remainder.
        return removeComments(line.substring(endIdx + 2), false);
    }

    // Not currently inside a block comment.
    const blockStart = line.indexOf('/*');
    const lineStart  = line.indexOf('//');

    // No comment at all → the whole line is "clean".
    if (blockStart === -1 && lineStart === -1) {
        return { cleanLine: line, stillInBlockComment: false };
    }

    // `//` comes first (or there is no `/*`).
    if (lineStart !== -1 && (blockStart === -1 || lineStart < blockStart)) {
        return { cleanLine: line.substring(0, lineStart), stillInBlockComment: false };
    }

    // `/*` comes first — consume until `*/`.
    const blockEnd = line.indexOf('*/', blockStart + 2);
    if (blockEnd !== -1) {
        // Block comment fully contained on this line; splice it out and
        // recurse in case another comment follows.
        const before = line.substring(0, blockStart);
        const after  = line.substring(blockEnd + 2);
        const rest = removeComments(before + after, false);
        return rest;
    }

    // Block comment starts here but doesn't end on this line.
    return { cleanLine: line.substring(0, blockStart), stillInBlockComment: true };
}

/**
 * Deactivate the extension.
 */
export function deactivate(): void {}
