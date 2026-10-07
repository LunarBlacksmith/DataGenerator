# DataGenerator pattern language

The pattern language describes the value of a column in plain English, for example:

```
P FOLLOWED BY SEQ(1-1000, 1) FOLLOWED BY (X OR Y) FOLLOWED BY (RAND_NUM(0, 99, 2))
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
- [Narrowing numbers](#narrowing-numbers)
- [Function arguments](#function-arguments)
- [Functions](#functions)
- [Column types](#column-types)
- [Row numbers and row sets](#row-numbers-and-row-sets)
- [Finding existing values with a pattern](#finding-existing-values-with-a-pattern)
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
5. To reuse a pattern in other columns, row sets or sessions, or to share it with colleagues, save it with the column's saved-settings button. See [Saved column settings](SavedSettings.md).
6. While you type, the functions and keywords that match the word being typed are suggested in a small list under it,
   e.g. typing `RAND` suggests `RAND_NUM`, `RAND_DATE` and the other `RAND_` functions. The list does not take the focus, so
   you can keep typing and ignore it. The suggestions also work in the **Try a pattern** box.

   | Key | What it does |
   |---|---|
   | ↑ / ↓ | Choose a suggestion. The function's arguments, description and an example are shown below the list. |
   | Tab, Tab | Insert the chosen suggestion. The first Tab arms it ("Press Tab again to insert …"), the second inserts it, so a single Tab never replaces a word by accident. A function is inserted with its brackets and the caret between them, e.g. `RAND_NUM()`, ready for the arguments; a keyword gets a space after it. Double-clicking a suggestion inserts it too. |
   | Esc | Close the suggestions. They come back when you type the next letter. |

   Suggestions appear once a word has two letters, and not inside quoted text.

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
- Keywords and function names are not case-sensitive: `followed by`, `Seq(...)` and `rand_num(...)` all work. Text keeps its case.

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

## Narrowing numbers

`GREATER THAN`, `LESS THAN`, `AT LEAST` and `AT MOST` narrow the numbers of a `NUM(...)`, `RAND_NUM(...)`, `ODD(...)` or `EVEN(...)` that comes straight before them.
They can be combined, and they bind more tightly than `REPEATED`, `OR` and `FOLLOWED BY`:

| Pattern                                             | Means                                        | Example values      |
| --------------------------------------------------- | -------------------------------------------- | ------------------- |
| `NUM(digits=5) GREATER THAN 50`                     | 51 to 99999, padded to 5 digits              | `00051`, `48213`    |
| `RAND_NUM(0, 999) LESS THAN 100`                    | 0 to 99                                      | `7`, `93`           |
| `NUM(digits=3) AT LEAST 100 AT MOST 199`            | 100 to 199                                   | `100`, `157`        |
| `S THEN (NUM(digits=5) GREATER THAN 50) THEN (1 OR 2)` | `S`, a number above 50, then `1` or `2`   | `S048211`, `S000522` |

- The number after the keyword is a whole number and may be negative (`GREATER THAN -10`).
- A comparison that leaves no numbers, e.g. `NUM(digits=2) GREATER THAN 99`, is reported as an error.
- The words `GREATER`, `LESS`, `THAN`, `AT`, `LEAST` and `MOST` are only keywords straight after a number function, so they can still be used as text elsewhere.

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

| Value      | Written as                                                         |
| ---------- | ------------------------------------------------------------------ |
| A number   | `5`, `-3`, `2.5`                                                   |
| A range    | `1-1000`, `1..1000`, `1 TO 1000`, or two numbers `1, 1000`         |
| Text       | Quoted (`'2024-12-31'`, `'yyyy-MM-dd'`) or a single word (`LOWER`) |
| A function | `TODAY(format='yyyy-MM-dd')`, `RAND_NUM(2, 5)`, `COL(Size)`        |

Ranges include both ends and are written smallest first. Negative numbers work in ranges too: `-50-50` and `-50 TO 50` are the same range.
`SEQ`, `RAND_NUM`, `ODD`, `EVEN` and `RAND_DECIMAL` also accept a named range, `range=1-1000`, or both ends named, `min=1, max=1000`. A length can be named too: `length=5-8`.

### Functions inside functions

Any argument can be another function call, including named arguments. The inner function is worked out first for every row and its value is used as the argument, as if it had been typed in:

| Pattern                                               | Means                                                      |
| ----------------------------------------------------- | ---------------------------------------------------------- |
| `RAND_DATE(TODAY(format='yyyy-MM-dd'), '2030-12-31')` | A random date from today to the end of 2030                |
| `RAND_NUM(1, RAND_NUM(2, 5))`                         | A random number from 1 to a maximum that is itself random  |
| `RAND_LETTERS(length=COL(NameLength))`                | As many letters as the `NameLength` column of the row says |
| `ONE_OF(TODAY(format='yyyy'), 'none')`                | This year or `none`                                        |

- A range cannot contain a function (`RAND_NUM(1-RAND_NUM(2, 5))`); write its ends as two arguments instead: `RAND_NUM(1, RAND_NUM(2, 5))`.
- The value of the inner function must suit the argument. For example, `RAND_DATE` needs a date, so `TODAY()` must use a format that reads as a date (its default `'yyyy-MM-dd HH:mm:ss'` and `'yyyy-MM-dd'` both work).

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

### RAND_NUM(range, digits)

A random whole number from the range, including both ends.

| Argument | Required | Default    | Notes                                                                      |
| -------- | -------- | ---------- | -------------------------------------------------------------------------- |
| `range`  | Yes      |            | Whole numbers, e.g. `0, 99` or `0-99`.                                     |
| `digits` | No       | No padding | Zero-pads the number to this width, e.g. `7` becomes `07` with `digits=2`. |

| Pattern               | Example values    |
| --------------------- | ----------------- |
| `RAND_NUM(1, 500)`    | `45`, `364`, `91` |
| `RAND_NUM(0, 99, 2)`  | `07`, `45`, `73`  |
| `RAND_NUM(-50 TO 50)` | `6`, `-37`, `28`  |

### ODD(range, digits) and EVEN(range, digits)

Random whole numbers of the specified parity within an inclusive range. Like `RAND_NUM`, both functions require a range
and accept an optional `digits` argument for zero padding. Zero is even, and negative ranges are supported for generation.
Every eligible value has the same chance of being chosen.

| Pattern                          | Possible values                       |
| -------------------------------- | ------------------------------------- |
| `ODD(0, 10)`                     | `1`, `3`, `5`, `7`, `9`                |
| `EVEN(1-10, digits=2)`            | `02`, `04`, `06`, `08`, `10`           |
| `ODD(min=-5, max=0)`             | `-5`, `-3`, `-1`                       |
| `EVEN(0, 0)`                     | `0`                                   |
| `ODD(1, 99) GREATER THAN 90`      | `91`, `93`, `95`, `97`, `99`           |
| `EVEN(0, RAND_NUM(4, 10))`        | Even values up to the nested maximum  |

A range or comparison with no eligible values, such as `ODD(2, 2)` or `EVEN(1, 2) LESS THAN 2`, reports a pattern error.
Non-negative ranges also work in lookup pattern filters; their SQL conditions check both the range and the last digit's parity.
As with other numeric functions, negative ranges cannot be used as SQL lookup filters.

### NUM(min, max, digits)

A random whole number like `RAND_NUM`, but every argument is optional, which makes it handy for "any number of n digits".
It can be narrowed with [GREATER THAN and the other comparisons](#narrowing-numbers).

| Argument | Required | Default       | Notes                                                                                     |
| -------- | -------- | ------------- | ----------------------------------------------------------------------------------------- |
| `range`  | No       | See `digits`  | Whole numbers, e.g. `1, 50` or `1-50`. With a range, `NUM` is the same as `RAND_NUM`.     |
| `digits` | No       | No padding    | Without a range, the number has up to this many digits and is zero-padded to that width. |

| Pattern                         | Range               | Example values     |
| ------------------------------- | ------------------- | ------------------ |
| `NUM()`                         | 0 to 999,999,999    | `48213977`, `5121` |
| `NUM(digits=5)`                 | 0 to 99999, 5 wide  | `00412`, `73001`   |
| `NUM(1, 50)`                    | 1 to 50             | `7`, `42`          |
| `NUM(digits=5) GREATER THAN 50` | 51 to 99999, 5 wide | `00051`, `48213`   |

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

Random digits 0 to 9. Unlike `RAND_NUM`, the result can start with zeros.

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

### TODAY(time, format)

Today's date on the computer that generates the data. By default it has the time at which the value is generated; `ANY` picks a random time of the day instead.

| Argument | Required | Default                 | Notes                                                                                                                                            |
| -------- | -------- | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| `time`   | No       | `NOW`                   | `NOW`: the date and time at the moment the value is generated. `ANY`: a random time from 00:00:00 to 23:59:59 today, picked again for every row. |
| `format` | No       | `'yyyy-MM-dd HH:mm:ss'` | A quoted [.NET date format][DATE-FORMATS].                                                                                                       |

| Pattern                                                  | Example values (generated on 14 March 2025 at 09:41:07) |
| -------------------------------------------------------- | ------------------------------------------------------- |
| `TODAY()`                                                | `2025-03-14 09:41:07`                                   |
| `TODAY(ANY)`                                             | `2025-03-14 17:03:52`, `2025-03-14 02:18:30`            |
| `TODAY(NOW, 'yyyy-MM-dd')`                               | `2025-03-14`                                            |
| `TODAY(time=ANY, format='dd/MM/yyyy HH:mm')`             | `14/03/2025 21:47`, `14/03/2025 06:05`                  |
| `'BATCH-' + TODAY(format='yyyyMMdd') + '-' + SEQ(1-999)` | `BATCH-20250314-001`, `BATCH-20250314-002`              |

The date is worked out when the data is generated. A generated SQL file contains those values, so running the file on a later day still inserts the day it was generated.

### ONE_OF(value, value, …)

Picks one of the listed values at random, each with the same chance. The values can be quoted text, words or numbers.
This works like `OR`, but any text can be listed without operators.

| Pattern                             | Example values         |
| ----------------------------------- | ---------------------- |
| `ONE_OF('XS', 'S', 'M', 'L', 'XL')` | `XS`, `L`, `M`         |
| `ONE_OF(Red, Green, Blue)`          | `Blue`, `Green`, `Red` |

### CYCLE(value, value, …)

Takes the listed values in turn instead of at random: the first row gets the first value, the second row the second value, and so on, starting again after the last value.

| Pattern                         | Rows 1 to 5                 |
| ------------------------------- | --------------------------- |
| `CYCLE('S', 'M', 'L')`          | `S`, `M`, `L`, `S`, `M`     |
| `'Bin ' + CYCLE(A, B) + ROW(3)` | `Bin A001`, `Bin B002`, …   |

### ROW(digits)

The number of the row within its row set: 1 for the first row, 2 for the second and so on. Unlike `SEQ` it has no range and never wraps.

| Argument | Required | Default    | Notes                                   |
| -------- | -------- | ---------- | --------------------------------------- |
| `digits` | No       | No padding | Zero-pads the number to this width.     |

`'ITEM-' + ROW(4)` produces `ITEM-0001`, `ITEM-0002`, …

### FIRST(value) and FIRST(n, value, …)

A value for the first row, or for each of the first `n` rows, of the row set. Every other row gets the `else` value, which is empty unless given.

| Form                          | Meaning                                                           |
| ----------------------------- | ----------------------------------------------------------------- |
| `FIRST(value)`                | `value` for the first row.                                        |
| `FIRST(n, value)`             | `value` for each of the first `n` rows.                           |
| `FIRST(n, v1, v2, …, vn)`     | `v1` for the first row, `v2` for the second, … (one value per row). |
| `…, else=value`               | The value of every other row. Optional, empty by default.         |

| Pattern                        | Rows 1 to 5 of 5           |
| ------------------------------ | -------------------------- |
| `FIRST('HEAD', else='-')`      | `HEAD`, `-`, `-`, `-`, `-` |
| `FIRST(2, 'A', 'B', else='-')` | `A`, `B`, `-`, `-`, `-`    |
| `'Row' + FIRST(3, '*')`        | `Row*`, `Row*`, `Row*`, `Row`, `Row` |

### LAST(value) and LAST(n, value, …)

The same as `FIRST`, counted from the end of the row set: a value for the last row, or one value for each of the last `n` rows in order, so the last listed value belongs to the very last row.
Every other row gets the `else` value (empty by default). In an update set, the rows are the rows being changed.

| Pattern                         | Rows 1 to 5 of 5           |
| ------------------------------- | -------------------------- |
| `LAST('END', else='-')`         | `-`, `-`, `-`, `-`, `END`  |
| `LAST(2, 'Y', 'Z', else='-')`   | `-`, `-`, `-`, `Y`, `Z`    |
| `LAST(3, 1, else=0)`            | `0`, `0`, `1`, `1`, `1`    |

- `n` is 1 to 1,000. With more than one value, the first one must be the plain number `n`, and there must be exactly `n` values or a single value for all of them.
- The **Try a pattern** box of the reference window has no row count, so it always shows the `else` value for `LAST`.

### GUID(case)

A new random GUID for every row, 36 characters including hyphens.

| Argument | Required | Default | Notes               |
| -------- | -------- | ------- | ------------------- |
| `case`   | No       | `UPPER` | `UPPER` or `LOWER`. |

`GUID()` produces values such as `8E535CA9-0931-41A6-96A9-14F37F118C97`. `GUID(LOWER)` produces values such as `1f384e23-2ec0-43d7-98bd-95870231bbc8`.

### COL(name)

The value of another column of the same row. `COLUMN(name)` means the same.

| Argument | Required | Default | Notes                                                                        |
| -------- | -------- | ------- | ---------------------------------------------------------------------------- |
| `name`   | Yes      |         | The column's name, as a word or quoted (`COL(Colour)`, `COL('Unit price')`). |

| Pattern                                   | Example values (when `Colour` is `Red` and `Size` is `42`) |
| ----------------------------------------- | ---------------------------------------------------------- |
| `COL(Colour) THEN 'aStringOfText' THEN 2` | `RedaStringOfText2`                                        |
| `COL(Colour) + '-' + COL(Size)`           | `Red-42`                                                   |
| `RAND_LETTERS(length=COL(Size))`          | 42 random letters                                          |

- The other column's value is used as text: numbers as plain digits with `.` for decimals (`12.5`), dates as `yyyy-MM-dd` (with ` HH:mm:ss` when they have a time), times as `HH:mm:ss`, `bit` as `1` or `0`, GUIDs in capitals with hyphens, binary as `0x…` hex, and NULL as empty text.
- The finished value is converted to the column's own type leniently, in the same way as the **Copy of column** generation mode (see [Column types](#column-types)), so `COL(...)` can be used in a number column even when the other column holds text.
- The other column is generated first, whatever its order in the table. Columns cannot use each other in a loop (`A` uses `B` and `B` uses `A`), and a column cannot use itself.
- A column whose value SQL Server sets while inserting (an identity, computed, `rowversion` or **Database generated** column) cannot be used, as its value is not known in advance.
- In a generated SQL script, a key column that refers to rows inserted by the same script has no value yet, so it cannot be used inside a pattern. Use the **Copy of column** mode to copy such a key.
- The **Try a pattern** box of the reference window has no row, so it shows `COL(Colour)` as `[Colour]`.

To copy another column's value exactly, without a pattern, set the **Generation mode** to **Copy of column** and pick the column in the **Settings** cell.

### Text functions: UPPER, LOWER, LEFT, RIGHT and PAD

These change a piece of text, most usefully the value of `COL(...)` or of another function.

| Function                        | Produces                                                                      | Example (`Colour` is `Red`, `Number` is `42`) |
| ------------------------------- | ----------------------------------------------------------------------------- | --------------------------------------------- |
| `UPPER(text)`                   | The text in capital letters.                                                  | `UPPER(COL(Colour))` → `RED`                  |
| `LOWER(text)`                   | The text in small letters.                                                    | `LOWER(COL(Colour))` → `red`                  |
| `LEFT(text, length)`            | The first `length` characters (all of the text when it is shorter).           | `LEFT(COL(Colour), 2)` → `Re`                 |
| `RIGHT(text, length)`           | The last `length` characters (all of the text when it is shorter).            | `RIGHT(COL(Colour), 2)` → `ed`                |
| `PAD(text, length, character)`  | The text with `character` (default `0`) in front until it is `length` long.   | `PAD(COL(Number), 6)` → `000042`              |

`length` is 0 to 1,000 and `character` must be a single character, e.g. `PAD(COL(Number), 6, ' ')`.

## Column types

A pattern always produces text, which is then stored in the column:

- **Text columns** (`char`, `varchar`, `nchar`, `nvarchar`, `text`, `ntext`, `sysname`) store the text as it is. A value longer than the column allows is reported as an error. It is never cut short.
- **Other columns** convert the text to the column's type, so the pattern must produce something that type accepts:

| Column type                                                        | The pattern must produce                   | For example                                             |
| ------------------------------------------------------------------ | ------------------------------------------ | ------------------------------------------------------- |
| `tinyint`, `smallint`, `int`, `bigint`                             | A whole number inside the type's range     | `RAND_NUM(1, 500)`                                      |
| `decimal`, `numeric`, `money`, `smallmoney`, `float`, `real`       | A number that fits the precision and scale | `RAND_DECIMAL(0, 999, 2)`                               |
| `bit`                                                              | `1`, `0`, `true`, `false`, `yes` or `no`   | `(1 OR 0)`                                              |
| `date`, `datetime`, `datetime2`, `smalldatetime`, `datetimeoffset` | A date in an unambiguous form              | `RAND_DATE('2024-01-01', '2024-12-31')`                 |
| `time`                                                             | A time of day                              | `RAND_NUM(8, 17, 2) + ':' + ONE_OF('00', '30') + ':00'` |
| `uniqueidentifier`                                                 | A GUID                                     | `GUID()`                                                |

For date columns, keep the default `yyyy-MM-dd` format or use `'yyyy-MM-dd HH:mm:ss'`. `TODAY()` already uses `'yyyy-MM-dd HH:mm:ss'`; for a `date` column, `TODAY(format='yyyy-MM-dd')` leaves the time out. A format such as `dd/MM/yyyy` is fine for text columns but can be misread as month/day in a date column.

### Values that use other columns

When a pattern uses `COL(...)`, or the column uses the **Copy of column** mode, the value is converted to the column's type leniently instead of being reported as an error:

| Column type                      | Conversion                                                                                                                                                                        |
| -------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Whole numbers                    | Decimals are cut to whole numbers. Text keeps only its digits (`'AB-12C3'` → `123`, a leading `-` keeps it negative); no digits → `0`. Values beyond the type's range are capped. |
| `decimal`, `numeric`, `money`, … | Rounded to the column's scale and capped to its precision. Text keeps only its digits and one decimal point.                                                                      |
| `bit`                            | `1` for a non-zero number or `true`/`yes`/`y`/`on`, otherwise `0`.                                                                                                                |
| Text                             | Cut to the column's length.                                                                                                                                                       |
| Dates                            | Text is read as a date; a number counts days from 1900-01-01. Text that is not a date uses its digits as days. Capped to the type's range.                                        |
| `time`                           | The time of a date, or text read as a time; otherwise 00:00:00.                                                                                                                   |
| `uniqueidentifier`               | Text read as a GUID; other text always gives the same GUID made from that text.                                                                                                   |
| `binary`, `varbinary`            | `0x…` hex text, a GUID's bytes or the text's UTF-8 bytes, cut to the column's length.                                                                                             |
| Any type                         | NULL stays NULL when the column allows NULL, otherwise the type's default (`0`, empty text, 1900-01-01, …).                                                                       |

## Row numbers and row sets

- `SEQ` counts by row number. Row numbers start again for each **row set**, so every row set starts its sequence at `start`.
  To carry on numbering in a second row set, give it a later `start`. For example, if the first set has 10 rows using `SEQ(1-9999)`, use `SEQ(1-9999, start=11)` in the second set.
- Random parts are picked independently for every row, so the same value can come up more than once.
  For a primary key or unique column, include a `SEQ(...)`, `ROW()` or `GUID()` so every value is different.
- `ROW`, `CYCLE`, `FIRST` and `LAST` also count by row number within the row set. `LAST` uses the row count of the row set
  (for an update set, the number of rows being changed).

## Finding existing values with a pattern

A pattern can also describe values to *look for*, in the `WHERE` part of a **Value from table** lookup or of an update set,
e.g. `Shirt.ShirtCode WHERE S THEN (NUM(digits=5) GREATER THAN 50) THEN (1 OR 2)`. The pattern is then turned into a SQL
condition that matches every value the pattern could produce. See [Steps, update sets and lookups](StepsUpdatesAndLookups.md).

- Only parts whose values can be recognised may be used: text, `OR`, `REPEATED`, `SEQ`, `NUM`, `RAND_NUM`, `ODD`, `EVEN` (with comparisons),
  `RAND_DIGITS`, `RAND_LETTERS`, `RAND_ALPHANUM`, `ONE_OF`, `CYCLE`, `FIRST`, `LAST` and `GUID`. Dates, decimals, `TODAY`, `ROW`, `COL` and
  the text functions cannot be used, and an error says so.
- Matching ignores letter case, as SQL Server usually does.
- A pattern with a very large number of possible shapes (for example many `OR`s inside a `REPEATED`) is reported as too complex; use a `REGEX` or `SQL` filter instead.

## Limits

| Item                                                  | Limit                               |
| ----------------------------------------------------- | ----------------------------------- |
| `REPEATED` count                                      | 0 to 1,000                          |
| `RAND_LETTERS`, `RAND_DIGITS`, `RAND_ALPHANUM` length | 0 to 1,000                          |
| `digits`                                              | 0 to 19                             |
| `decimals`                                            | 0 to 10                             |
| Whole numbers in `SEQ`, `NUM`, `RAND_NUM`, `ODD` and `EVEN` | -10<sup>18</sup> to 10<sup>18</sup> |
| `NUM` `digits` without a range                        | 0 to 18                             |
| `FIRST` and `LAST` row count                          | 1 to 1,000                          |
| `LEFT`, `RIGHT` and `PAD` length                      | 0 to 1,000                          |
| One generated value                                   | 100,000 characters                  |

## Errors

Problems are reported as you type, with the position of the problem counted in characters from 1:

```
Expected BY after FOLLOWED. (at position 12)
```

Common problems:

| Pattern                             | Problem and fix                                                                                                                           |
| ----------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `P SEQ(1-10)`                       | Parts must be joined: `P FOLLOWED BY SEQ(1-10)` or `P + SEQ(1-10)`.                                                                       |
| `'ID' + -`                          | Symbols must be quoted: `'ID' + '-'`.                                                                                                     |
| `TO`                                | Keywords must be quoted to be used as text: `'TO'`.                                                                                       |
| `SEQ(1-10`                          | The function is missing its closing `)`.                                                                                                  |
| `RAND_DATE(2024-01-01, 2024-12-31)` | Dates must be quoted: `RAND_DATE('2024-01-01', '2024-12-31')`.                                                                            |
| `FOO(1)`                            | There is no function called `FOO`. Quote the word if you mean text: `'FOO(1)'`.                                                           |
| `SEQ(10-1)`                         | The range is reversed. Write the smaller number first: `SEQ(1-10)`. To count down, use `step=-1`.                                         |
| `SEQ(1-1000, digits=2)`             | 1000 needs 4 digits. Use `digits=4` or more, or `digits=0` for no padding.                                                                |
| `RAND_LETTERS(3, case=TITLE)`       | `case` must be `UPPER`, `LOWER` or `MIXED`.                                                                                               |
| `TODAY('dd/MM/yyyy')`               | The first value is the time, `NOW` or `ANY`. Put the format second, `TODAY(NOW, 'dd/MM/yyyy')`, or name it: `TODAY(format='dd/MM/yyyy')`. |
| `RAND_NUM(1-RAND_NUM(2, 5))`        | A range cannot contain a function. Write the ends as two arguments: `RAND_NUM(1, RAND_NUM(2, 5))`.                                        |
| `COL(Colour)` on `Colour` itself    | A column cannot use its own value, and columns cannot use each other in a loop. Pick another column.                                      |
| `ONE_OF(A, B) GREATER THAN 5`       | Comparisons only follow `NUM(...)`, `RAND_NUM(...)`, `ODD(...)` or `EVEN(...)`: `NUM(0, 99) GREATER THAN 5`.                                |
| `LAST(2, 'A', 'B', 'C')`            | Two rows need two values (or one for both): `LAST(2, 'B', 'C')` or `LAST(3, 'A', 'B', 'C')`.                                              |

## Examples

| Purpose           | Pattern                                                                            | Example values                               |
| ----------------- | ---------------------------------------------------------------------------------- | -------------------------------------------- |
| Product code      | `P FOLLOWED BY SEQ(1-1000, 1) FOLLOWED BY (X OR Y) FOLLOWED BY RAND_NUM(0, 99, 2)` | `P0001Y81`, `P0002X53`                       |
| Asset tag         | `'ASSET-' FOLLOWED BY SEQ(1-999999, digits=6)`                                     | `ASSET-000001`, `ASSET-000002`               |
| Customer name     | `'Customer ' + SEQ(1-100000, digits=0)`                                            | `Customer 1`, `Customer 2`                   |
| Bin location      | `RAND_LETTERS(2) + '-' + RAND_DIGITS(3) + '-' + (A OR B OR C)`                     | `WE-542-B`, `CP-687-C`                       |
| Shirt size        | `ONE_OF('XS', 'S', 'M', 'L', 'XL')`                                                | `XS`, `M`                                    |
| Order number      | `'ORD' + RAND_DATE('2024-01-01', '2024-12-31', 'yyyyMMdd') + '-' + SEQ(1-9999)`    | `ORD20240605-0001`, `ORD20241127-0002`       |
| Batch of today    | `'BATCH-' + TODAY(format='yyyyMMdd') + '-' + SEQ(1-999)`                           | `BATCH-20250314-001`, `BATCH-20250314-002`   |
| Created today     | `TODAY(ANY)`                                                                       | `2025-03-14 17:03:52`, `2025-03-14 02:18:30` |
| Due from today    | `RAND_DATE(TODAY(format='yyyy-MM-dd'), '2030-12-31')`                              | `2027-08-19`, `2025-11-02`                   |
| Based on a column | `COL(Colour) THEN '-' THEN SEQ(1-999)`                                             | `Red-001`, `Blue-002`                        |
| Short colour code | `UPPER(LEFT(COL(Colour), 3)) + PAD(ROW(), 4)`                                      | `RED0001`, `BLU0002`                         |
| Rotating sizes    | `CYCLE('S', 'M', 'L')`                                                             | `S`, `M`, `L`, `S`                           |
| Closing row       | `'Line ' + ROW() + LAST(' (final)')`                                               | `Line 1`, …, `Line 10 (final)`               |
| Number above 50   | `S THEN (NUM(digits=5) GREATER THAN 50) THEN (1 OR 2)`                             | `S048211`, `S000522`                         |
| E-mail address    | `RAND_LETTERS(5-8, LOWER) + '.' + RAND_LETTERS(6, LOWER) + '@example.com'`         | `sogxi.blqmdj@example.com`                   |
| Australian mobile | `'04' FOLLOWED BY RAND_DIGITS(8)`                                                  | `0406793209`, `0465689056`                   |
| Licence plate     | `RAND_LETTERS(3) + '-' + (RAND_DIGITS(1) OR RAND_LETTERS(1)) REPEATED 3 TIMES`     | `PDS-5E7`, `JZZ-QDN`                         |

## Grammar

For reference, the full syntax in EBNF. Keywords and function names are case-insensitive.

```ebnf
pattern       = concatenation ;
concatenation = choice , { ( "FOLLOWED" , "BY" | "THEN" | "+" ) , choice } ;
choice        = repetition , { ( "OR" | "|" ) , repetition } ;
repetition    = comparison , { "REPEATED" , count , [ ( "TO" | "-" | ".." ) , count ] , [ "TIMES" ] } ;
comparison    = primary , { ( "GREATER" , "THAN" | "LESS" , "THAN" | "AT" , "LEAST" | "AT" , "MOST" ) , [ "-" ] , count } ;  (* after NUM, RAND_NUM, ODD or EVEN only *)
primary       = quoted-text | number | word | function | "(" , concatenation , ")" ;
function      = word , "(" , [ argument , { "," , argument } ] , ")" ;
argument      = [ word , ( "=" | ":" ) ] , value ;
value         = function | quoted-text | word | signed-number , [ ( "-" | ".." | "TO" ) , signed-number ] ;
signed-number = [ "-" ] , number ;
number        = digit , { digit } , [ "." , digit , { digit } ] ;
word          = ( letter | digit | "_" ) , { letter | digit | "_" } ;  (* containing at least one letter or "_" *)
quoted-text   = "'" , { character } , "'" | '"' , { character } , '"' ;  (* a doubled quote stands for one quote *)
count         = digit , { digit } ;  (* 0 to 1000 *)
```

[DATE-FORMATS]: https://learn.microsoft.com/dotnet/standard/base-types/custom-date-and-time-format-strings