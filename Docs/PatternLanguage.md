# DataGenerator pattern language

The pattern language describes the value of a column in plain English, for example:

```
P FOLLOWED BY SEQ(1-1000, 1) FOLLOWED BY (X OR Y) FOLLOWED BY (RAND_NUMBER(0, 99, 2))
```

| Row | Example value |
| --- | ------------- |
| 1   | `P0001Y73`    |
| 2   | `P0002X04`    |
| 548 | `P0548X41`    |

That reads: the letter **P**, followed by a **sequence** from 1 to 1000 that starts at 1 (padded to 4 digits), followed by **X or Y** picked at random, followed by a **random number** from 0 to 99 shown with 2 digits.
The sequence moves on by one for every row; the random parts are picked again for every row.

## Contents

- [Using patterns in the app](#using-patterns-in-the-app)
- [Building blocks](#building-blocks)
- [Combining parts](#combining-parts)
- [Precedence and parentheses](#precedence-and-parentheses)
- [Function arguments](#function-arguments)
- [Functions](#functions)
- [Column types](#column-types)
- [Row numbers and row sets](#row-numbers-and-row-sets)
- [Limits](#limits)
- [Errors](#errors)
- [Examples](#examples)
- [Grammar](#grammar)

## Using patterns in the app

1. In the **Column rules** grid, set a column's **Generation mode** to **Pattern**.
2. Type the pattern into the **Settings** cell. The **Sample values** column previews the first rows as you type.
3. Mistakes are outlined in red. Hover over the box to see what is wrong and where.
4. The **?** button next to the box opens the **Pattern language reference** window with your pattern already loaded in **Try a pattern**.
   The **Pattern language** button below the grid opens the same window. Its **Try it** buttons load ready-made examples.

## Building blocks

A pattern is made of parts joined by [operators](#combining-parts). A part can be:

| Part        | Written as                      | Produces                                          |
| ----------- | ------------------------------- | ------------------------------------------------- |
| A word      | `P`, `ABC`, `Size_L`            | The word exactly as written                       |
| A number    | `2024`, `007`, `3.5`            | The number exactly as written (`007` stays `007`) |
| Quoted text | `'ID-'`, `"Unit 4"`, `' '`      | The text between the quotes                       |
| A function  | `SEQ(1-1000)`, `RAND_DIGITS(4)` | A value worked out for each row                   |
| A group     | `(X OR Y)`                      | Whatever the pattern inside produces              |

Rules for text:

- Spaces between parts are ignored: `P FOLLOWED BY 1` produces `P1`. To include a space, quote it: `P + ' ' + 1` produces `P 1`.
- Words may contain letters, digits and `_`. Anything else (spaces, `-`, `.`, `@`, `/`, `#` and so on) must be quoted: `'ID-'`, `'@example.com'`.
- Use single or double quotes. To put the quote character itself inside the text, double it or use the other kind of quote: `'O''Brien'` and `"O'Brien"` both produce `O'Brien`.
- The keywords `FOLLOWED`, `BY`, `THEN`, `OR`, `REPEATED`, `TO` and `TIMES` must be quoted to be used as text: `'OR'`.
- A word followed by `(` is a function call. Quote the word if you mean text: `'Box' + '(' + 1 + ')'`.
- Keywords and function names are not case-sensitive: `followed by`, `Seq(...)` and `rand_number(...)` all work. Text keeps its case.

## Combining parts

| Operator                  | Meaning                                                              | Example                     | Example output |
| ------------------------- | -------------------------------------------------------------------- | --------------------------- | -------------- |
| `A FOLLOWED BY B`         | A then B. `THEN` and `+` mean the same.                              | `'ID-' + RAND_DIGITS(4)`    | `ID-9685`      |
| `A OR B`                  | Picks A or B at random for each row. `\|` means the same.            | `(Red OR Green OR Blue)`    | `Green`        |
| `A REPEATED n TIMES`      | A, n times. `TIMES` is optional.                                     | `(X OR Y) REPEATED 3 TIMES` | `YXY`          |
| `A REPEATED n TO m TIMES` | A, a random number of times from n to m. `n-m` and `n..m` also work. | `'AB' REPEATED 1 TO 3`      | `ABAB`         |
| `( … )`                   | Groups parts so an operator applies to all of them.                  | `(A + B) REPEATED 3`        | `ABABAB`       |

Every repetition is worked out again, so `RAND_DIGITS(1) REPEATED 3 TIMES` produces three independent digits, and `(X OR Y) REPEATED 3 TIMES` can produce `XYX`.
A count of 0 leaves the part out.

`OR` gives every option in its list the same chance. `A OR B OR C` picks each letter a third of the time, but `(A OR B) OR C` picks `C` half the time.

## Precedence and parentheses

When parts are not grouped with parentheses, the operators are applied in this order:

1. `REPEATED`
2. `OR` (and `|`)
3. `FOLLOWED BY` (and `THEN`, `+`)

So:

| Pattern                              | Means                                | Example output |
| ------------------------------------ | ------------------------------------ | -------------- |
| `A FOLLOWED BY B OR C`               | `A FOLLOWED BY (B OR C)`             | `AB` or `AC`   |
| `X REPEATED 2 TIMES OR Y`            | `(X REPEATED 2 TIMES) OR Y`          | `XX` or `Y`    |
| `A FOLLOWED BY B REPEATED 3 TIMES`   | `A FOLLOWED BY (B REPEATED 3 TIMES)` | `ABBB`         |
| `(A FOLLOWED BY B) REPEATED 3 TIMES` | Repeats the pair                     | `ABABAB`       |

This matches how the pattern reads aloud: "P followed by X or Y" means P, then either X or Y.
When in doubt, put parentheses around the parts that belong together.

## Function arguments

Arguments are separated by commas. A function can take:

- **Positional arguments**, in the order shown in its signature. Optional arguments can be left off from the end.
- **Named arguments**, written `name=value` or `name:value`, after all positional arguments. Each name can be given once.

```
SEQ(1-999999, 1, 1, 6)        positional
SEQ(1-999999, digits=6)       start and step keep their defaults
RAND_LETTERS(5, case=LOWER)   mixed
```

Argument values can be:

| Value    | Written as                                                         |
| -------- | ------------------------------------------------------------------ |
| A number | `5`, `-3`, `2.5`                                                   |
| A range  | `1-1000`, `1..1000`, `1 TO 1000`, or two numbers `1, 1000`         |
| Text     | Quoted (`'2024-12-31'`, `'yyyy-MM-dd'`) or a single word (`LOWER`) |

Ranges include both ends and are written smallest first. Negative numbers work in ranges too: `-50-50` and `-50 TO 50` are the same range.
`SEQ`, `RAND_NUMBER` and `RAND_DECIMAL` also accept a named range, `range=1-1000`, or both ends named, `min=1, max=1000`. A length can be named too: `length=5-8`.

## Functions

### SEQ(range, start, step, digits)

Counts through the range, one step per row. After the end of the range it wraps back to the start of the range. `SEQUENCE` is an alias.

| Argument | Required | Default                                | Notes                                                                   |
| -------- | -------- | -------------------------------------- | ----------------------------------------------------------------------- |
| `range`  | Yes      |                                        | Whole numbers, e.g. `1-1000`.                                           |
| `start`  | No       | The first number of the range          | The value of the first row. Must be inside the range.                   |
| `step`   | No       | `1`                                    | Added for each row. Use a negative number to count down. Cannot be `0`. |
| `digits` | No       | The number of digits of the widest end | Zero-pads every value to this width. `0` turns padding off.             |

| Pattern                      | Rows 1 to 4                                    |
| ---------------------------- | ---------------------------------------------- |
| `SEQ(1-1000)`                | `0001`, `0002`, `0003`, `0004`                 |
| `SEQ(1-1000, digits=0)`      | `1`, `2`, `3`, `4`                             |
| `SEQ(1-999999, digits=8)`    | `00000001`, `00000002`, `00000003`, `00000004` |
| `SEQ(10-50, step=10)`        | `10`, `20`, `30`, `40`                         |
| `SEQ(1-5, start=4, step=-1)` | `4`, `3`, `2`, `1` (then `5`, `4`, …)          |

`digits` must be at least as wide as the widest number in the range, so every value has the same width. Negative numbers keep their minus sign in front of the padding: `-05`.

### RAND_NUMBER(range, digits)

A random whole number from the range, including both ends.

| Argument | Required | Default    | Notes                                                                      |
| -------- | -------- | ---------- | -------------------------------------------------------------------------- |
| `range`  | Yes      |            | Whole numbers, e.g. `0, 99` or `0-99`.                                     |
| `digits` | No       | No padding | Zero-pads the number to this width, e.g. `7` becomes `07` with `digits=2`. |

| Pattern                  | Example values    |
| ------------------------ | ----------------- |
| `RAND_NUMBER(1, 500)`    | `45`, `364`, `91` |
| `RAND_NUMBER(0, 99, 2)`  | `07`, `45`, `73`  |
| `RAND_NUMBER(-50 TO 50)` | `6`, `-37`, `28`  |

### RAND_DECIMAL(range, decimals)

A random number from the range, rounded to a fixed number of decimal places.

| Argument   | Required | Default | Notes                                           |
| ---------- | -------- | ------- | ----------------------------------------------- |
| `range`    | Yes      |         | May include decimals, e.g. `0.5-9.5`.           |
| `decimals` | No       | `2`     | 0 to 10. Trailing zeros are kept, e.g. `23.00`. |

| Pattern                             | Example values            |
| ----------------------------------- | ------------------------- |
| `RAND_DECIMAL(0, 100)`              | `65.36`, `23.00`, `49.21` |
| `RAND_DECIMAL(0.5-9.5, 1)`          | `1.9`, `8.0`, `8.1`       |
| `RAND_DECIMAL(1, 1000, decimals=0)` | `812`, `454`, `866`       |

### RAND_LETTERS(length, case)

Random letters A to Z.

| Argument | Required | Default | Notes                                                              |
| -------- | -------- | ------- | ------------------------------------------------------------------ |
| `length` | Yes      |         | A number of letters (`3`), or a range for a random length (`5-8`). |
| `case`   | No       | `UPPER` | `UPPER`, `LOWER` or `MIXED`.                                       |

| Pattern                       | Example values                  |
| ----------------------------- | ------------------------------- |
| `RAND_LETTERS(3)`             | `SMJ`, `VZV`, `CEN`             |
| `RAND_LETTERS(5-8, LOWER)`    | `pwgyaicm`, `gydtgkg`, `yhmzde` |
| `RAND_LETTERS(4, case=MIXED)` | `slOE`, `nTlm`, `igQd`          |

### RAND_DIGITS(length)

Random digits 0 to 9. Unlike `RAND_NUMBER`, the result can start with zeros.

| Argument | Required | Default | Notes                                   |
| -------- | -------- | ------- | --------------------------------------- |
| `length` | Yes      |         | A number of digits, or a range (`4-6`). |

`RAND_DIGITS(4)` produces values such as `0248`, `8899` and `0277`.

### RAND_ALPHANUM(length, case)

Random letters and digits. The arguments are the same as for `RAND_LETTERS`.

`RAND_ALPHANUM(8)` produces values such as `OA5F3NK8` and `HZWRD633`. `RAND_ALPHANUM(6, LOWER)` produces values such as `cd18hh` and `b0mixx`.

### RAND_DATE(min, max, format)

A random date (and time) between two dates, including both ends.

| Argument | Required | Default        | Notes                                                                        |
| -------- | -------- | -------------- | ---------------------------------------------------------------------------- |
| `min`    | Yes      |                | A quoted date, optionally with a time: `'2024-01-01'`, `'2024-01-01 08:00'`. |
| `max`    | Yes      |                | As above. A date without a time includes that whole day.                     |
| `format` | No       | `'yyyy-MM-dd'` | A quoted [.NET date format][DATE-FORMATS].                                   |

Useful format codes: `yyyy` year, `MM` month, `dd` day, `HH` hour (24-hour), `mm` minute, `ss` second.

| Pattern                                                                                | Example values                         |
| -------------------------------------------------------------------------------------- | -------------------------------------- |
| `RAND_DATE('2024-01-01', '2024-12-31')`                                                | `2024-01-18`, `2024-06-05`             |
| `RAND_DATE('2024-01-01', '2024-01-31', 'dd/MM/yyyy')`                                  | `03/01/2024`, `15/01/2024`             |
| `RAND_DATE(min='2024-01-01 08:00', max='2024-01-01 17:00', format='yyyy-MM-dd HH:mm')` | `2024-01-01 08:27`, `2024-01-01 12:09` |

Dates must be quoted. Without quotes, `2024-01-01` would be read as a range of numbers.

### ONE_OF(value, value, …)

Picks one of the listed values at random, each with the same chance. The values can be quoted text, words or numbers.
This works like `OR`, but any text can be listed without operators.

| Pattern                             | Example values         |
| ----------------------------------- | ---------------------- |
| `ONE_OF('XS', 'S', 'M', 'L', 'XL')` | `XS`, `L`, `M`         |
| `ONE_OF(Red, Green, Blue)`          | `Blue`, `Green`, `Red` |

### GUID(case)

A new random GUID for every row, 36 characters including hyphens.

| Argument | Required | Default | Notes               |
| -------- | -------- | ------- | ------------------- |
| `case`   | No       | `UPPER` | `UPPER` or `LOWER`. |

`GUID()` produces values such as `8E535CA9-0931-41A6-96A9-14F37F118C97`. `GUID(LOWER)` produces values such as `1f384e23-2ec0-43d7-98bd-95870231bbc8`.

## Column types

A pattern always produces text, which is then stored in the column:

- **Text columns** (`char`, `varchar`, `nchar`, `nvarchar`, `text`, `ntext`, `sysname`) store the text as it is. A value longer than the column allows is reported as an error. It is never cut short.
- **Other columns** convert the text to the column's type, so the pattern must produce something that type accepts:

| Column type                                                        | The pattern must produce                   | For example                                                |
| ------------------------------------------------------------------ | ------------------------------------------ | ---------------------------------------------------------- |
| `tinyint`, `smallint`, `int`, `bigint`                             | A whole number inside the type's range     | `RAND_NUMBER(1, 500)`                                      |
| `decimal`, `numeric`, `money`, `smallmoney`, `float`, `real`       | A number that fits the precision and scale | `RAND_DECIMAL(0, 999, 2)`                                  |
| `bit`                                                              | `1`, `0`, `true`, `false`, `yes` or `no`   | `(1 OR 0)`                                                 |
| `date`, `datetime`, `datetime2`, `smalldatetime`, `datetimeoffset` | A date in an unambiguous form              | `RAND_DATE('2024-01-01', '2024-12-31')`                    |
| `time`                                                             | A time of day                              | `RAND_NUMBER(8, 17, 2) + ':' + ONE_OF('00', '30') + ':00'` |
| `uniqueidentifier`                                                 | A GUID                                     | `GUID()`                                                   |

For date columns, keep the default `yyyy-MM-dd` format or use `'yyyy-MM-dd HH:mm:ss'`. A format such as `dd/MM/yyyy` is fine for text columns but can be misread as month/day in a date column.

## Row numbers and row sets

- `SEQ` counts by row number. Row numbers start again for each **row set**, so every row set starts its sequence at `start`.
  To carry on numbering in a second row set, give it a later `start`. For example, if the first set has 10 rows using `SEQ(1-9999)`, use `SEQ(1-9999, start=11)` in the second set.
- Random parts are picked independently for every row, so the same value can come up more than once.
  For a primary key or unique column, include a `SEQ(...)` or `GUID()` so every value is different.

## Limits

| Item                                                  | Limit                               |
| ----------------------------------------------------- | ----------------------------------- |
| `REPEATED` count                                      | 0 to 1,000                          |
| `RAND_LETTERS`, `RAND_DIGITS`, `RAND_ALPHANUM` length | 0 to 1,000                          |
| `digits`                                              | 0 to 19                             |
| `decimals`                                            | 0 to 10                             |
| Whole numbers in `SEQ` and `RAND_NUMBER`              | -10<sup>18</sup> to 10<sup>18</sup> |
| One generated value                                   | 100,000 characters                  |

## Errors

Problems are reported as you type, with the position of the problem counted in characters from 1:

```
Expected BY after FOLLOWED. (at position 12)
```

Common problems:

| Pattern                             | Problem and fix                                                                                   |
| ----------------------------------- | ------------------------------------------------------------------------------------------------- |
| `P SEQ(1-10)`                       | Parts must be joined: `P FOLLOWED BY SEQ(1-10)` or `P + SEQ(1-10)`.                               |
| `'ID' + -`                          | Symbols must be quoted: `'ID' + '-'`.                                                             |
| `TO`                                | Keywords must be quoted to be used as text: `'TO'`.                                               |
| `SEQ(1-10`                          | The function is missing its closing `)`.                                                          |
| `RAND_DATE(2024-01-01, 2024-12-31)` | Dates must be quoted: `RAND_DATE('2024-01-01', '2024-12-31')`.                                    |
| `FOO(1)`                            | There is no function called `FOO`. Quote the word if you mean text: `'FOO(1)'`.                   |
| `SEQ(10-1)`                         | The range is reversed. Write the smaller number first: `SEQ(1-10)`. To count down, use `step=-1`. |
| `SEQ(1-1000, digits=2)`             | 1000 needs 4 digits. Use `digits=4` or more, or `digits=0` for no padding.                        |
| `RAND_LETTERS(3, case=TITLE)`       | `case` must be `UPPER`, `LOWER` or `MIXED`.                                                       |

## Examples

| Purpose           | Pattern                                                                               | Example values                         |
| ----------------- | ------------------------------------------------------------------------------------- | -------------------------------------- |
| Product code      | `P FOLLOWED BY SEQ(1-1000, 1) FOLLOWED BY (X OR Y) FOLLOWED BY RAND_NUMBER(0, 99, 2)` | `P0001Y81`, `P0002X53`                 |
| Asset tag         | `'ASSET-' FOLLOWED BY SEQ(1-999999, digits=6)`                                        | `ASSET-000001`, `ASSET-000002`         |
| Customer name     | `'Customer ' + SEQ(1-100000, digits=0)`                                               | `Customer 1`, `Customer 2`             |
| Bin location      | `RAND_LETTERS(2) + '-' + RAND_DIGITS(3) + '-' + (A OR B OR C)`                        | `WE-542-B`, `CP-687-C`                 |
| Shirt size        | `ONE_OF('XS', 'S', 'M', 'L', 'XL')`                                                   | `XS`, `M`                              |
| Order number      | `'ORD' + RAND_DATE('2024-01-01', '2024-12-31', 'yyyyMMdd') + '-' + SEQ(1-9999)`       | `ORD20240605-0001`, `ORD20241127-0002` |
| E-mail address    | `RAND_LETTERS(5-8, LOWER) + '.' + RAND_LETTERS(6, LOWER) + '@example.com'`            | `sogxi.blqmdj@example.com`             |
| Australian mobile | `'04' FOLLOWED BY RAND_DIGITS(8)`                                                     | `0406793209`, `0465689056`             |
| Licence plate     | `RAND_LETTERS(3) + '-' + (RAND_DIGITS(1) OR RAND_LETTERS(1)) REPEATED 3 TIMES`        | `PDS-5E7`, `JZZ-QDN`                   |

## Grammar

For reference, the full syntax in EBNF. Keywords and function names are case-insensitive.

```ebnf
pattern       = concatenation ;
concatenation = choice , { ( "FOLLOWED" , "BY" | "THEN" | "+" ) , choice } ;
choice        = repetition , { ( "OR" | "|" ) , repetition } ;
repetition    = primary , { "REPEATED" , count , [ ( "TO" | "-" | ".." ) , count ] , [ "TIMES" ] } ;
primary       = quoted-text | number | word | function | "(" , concatenation , ")" ;
function      = word , "(" , [ argument , { "," , argument } ] , ")" ;
argument      = [ word , ( "=" | ":" ) ] , value ;
value         = quoted-text | word | signed-number , [ ( "-" | ".." | "TO" ) , signed-number ] ;
signed-number = [ "-" ] , number ;
number        = digit , { digit } , [ "." , digit , { digit } ] ;
word          = ( letter | digit | "_" ) , { letter | digit | "_" } ;  (* containing at least one letter or "_" *)
quoted-text   = "'" , { character } , "'" | '"' , { character } , '"' ;  (* a doubled quote stands for one quote *)
count         = digit , { digit } ;  (* 0 to 1000 *)
```

[DATE-FORMATS]: https://learn.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings