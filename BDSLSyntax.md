# BDSL 语言语法

`BDSL`语言是为项目`https://github.com/NeonNickNick/HighPerformanceBlacksmith.git`设计的一种技能DSL语言，具有简洁的语法和整齐的格式，并配套VSCode扩展。使用这一语言可以在不编写任何代码的情况下实现相当一部分职业技能。

## 1.创建一个BDSL语言文件
BDSL语言需要写在扩展名为`.bdsl`的文件中，这样插件才能识别它，并给出自动格式化和语法检查。

## 2.BDSL语言特点
BDSL语言语法设计参考了C++和Python，但是不需要分号也不需要缩进，换言之所有内容都可以写到同一行。设计原则是低自由度，符号简洁，要达成某个特定目的只有唯一一种写法。

## 3.注释
使用`//`进行单行注释，或使用配对的`/*`与`*/`进行多行注释。注释中的内容会被直接忽略。

## 4.关键字
打铁的每个职业具备的要素是：职业名、技能。每个技能具有两部分逻辑：检查技能能否使用和技能具体效果是什么。在BDSL语言中，这些要素的编写由关键字唤起：
```bdsl
[blockname]([param1name]param1 [param2name]param2)->{content}
```
唤起的内容称为一个Block。其中，`[blockname]`是关键字。`()`中是若干参数，在仅有一个参数的情况下，必须省略`[paramname]`；参数多余1个的情况下，不允许省略。`->`是必须的。`{}`内是对应要素的具体内容。

在BDSL语言中，`[]`有两种作用。第一种是作为关键字标识，第二种是作为参数名标识。作为约定，`[]`中间的内容不能自定义，否则解析时很难判断具体含义。

目前总共有四个关键字：

- `[skillpackagedefinition]`

    - 此关键字表明开始编写一个职业。
    - 在每个`.bdsl`文件中，此关键字必须出现且仅出现1次，即每个文件对应单一职业。
    - 每个.bdsl文件中不允许存在`[skillpackagedefinition]`Block之外的内容。
    - 唤起的Block的`()`内必须且仅能写入可自定义的职业名`"professionname"`。注意必须要有双引号，表示字符串。
- `[skill]`
    - 此关键字表明开始编写技能。
    - 此关键字只能出现在`[skillpackagedefinition]`Block的content部分。
    - 如果是被动技能，括号内需要且仅能写入`"auto"`。注意`"auto"`必须有双引号，表示字符串。如果不是，括号内写入技能名字符串。
- `[check]`
    - 此关键字表明开始编写检查技能是否能使用的逻辑。
    - 此关键字只能出现在非`"auto"`的`[skill]`Block的content部分，且在每个这样的`[skill]`Block必须出现且仅出现1次。
    - 括号内可以且仅能写入`!`。其含义见`5.逻辑语句`。
- `[declare]`
    - 此关键字表明开始编写具体技能逻辑。
    - 此关键字必须在所有`[skill]`Block的content部分出现，且对每个`[skill]`Block只能出现1次。

作为示例，一个.bdsl文件的骨架为
```bdsl
[skillpackagedefinition]("ExampleProfession")->{

    [skill]("ExampleSkill1")->{
        [check]()->{

        }
        [declare]()->{
           
        }
    }

    [skill]("ExampleSkill2")->{
        [check]()->{

        }
        [declare]()->{
           
        }
    }

    // ...
    /*...
    ...
    */
}

```
## 5.逻辑语句
检查一个技能是否可用时，需要做一系列的逻辑判断。在BDSL中，逻辑判断由`<require>`唤起：
```bdsl
<require> Iron >= 3
<require> HP * 2 >= 4
```
其含义是简单明了的，第一行表示必须有至少3个铁，第二行表示必须至少有2点生命。由此规定标准的语法为:

- `<require>`只能出现在`[check]`Block的content内。
- 每个`<require>`后面必须且只允许有一个结果为true或false的逻辑表达式，支持的比较算符只有`==`、`!=`、`>=`、`<=`。换言之，每个`<require>`后面必须且只有一个包含在上述四个之中的比较算符。
- 这样的算符左右必须都是合法的算数表达式，只有数字和算数运算符。Iron、HP等也被视为数字。支持的算数运算符只有`+`、`-`、`*`、`/`、`^`，且满足四则运算法则+乘方最优先。被视作数字的特殊名字有:
    - HP生命值
    - MHP最大生命值
    - Iron铁数（包含金铁）
    - GoldIron金铁数
    - Space空间数
    - Time时间数
    - Magic魔法数
    - \_\_skillparam\_\_技能层数（例如魔法2）
- 所有`<require>`语句的结果全部为true时，才表示通过检查。
- 没有任何`<require>`语句时视为通过检查。

前文提到，`[check]`Block的小括号内可以写入`!`，其含义是对内部所有`<require>`语句汇总后的结果取反。

值得注意的是，这样的规则不足以表达所有的检查逻辑（不能完全替代或运算），但是对于这个项目已经完全够用了，大部分情况下，不需要`!`。

作为示例：
```bdsl
[check]()->{
    <require> Iron >= 1 + __skillparam__ * 0.5
}
```

## 6.DSL语句
编写技能的具体逻辑时，要使用DSL发起标志。目前支持的标志有：

- `<attack>`攻击
- `<defense>`防御
- `<resource>`资源获取
- `<use>`消耗资源或生命或最大生命值
- `<takemark>`查询标记层数

标准语法为：
- `<dslmark>`只能出现在`[declare]`Block的content内。
- `<dslmark>`后由两部分内容，第一部分在`()`内，不可省略，表示每个语句必备的参数，第二部分在括号外，可以省略，表示可选参数。具体写法与每个标志有关。

作为示例：
```bdsl
[declare]()->{
    <takemark> @lazynum@
    <use> @lazynum@ Iron
    <use> __skillparam__ * 0.5 Iron
    <attack>([power]5 [type]Physical) [delay]0 [undo](!)->{
        <require> Space >= 1
    }
    <defense>(
        [name]"Shield"
        [analyzer]DefaultReduction
        [type]CommonReduction
        [power]2 + __skillparam__
        [clock](
            [delay]0
            [remain]1
            [isinfinite]false
        )
    ) [delay]0 [undo](!)->{
        <require> Space >= 1
    }
}
```
