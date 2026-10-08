# Pattern / Regex / SQL expression builder

Open **Expression builder** from the main window's status bar. It is a separate, modeless window and needs no database
connection. Feed it an explicit description of a code's segments, then choose **Build and save**. The builder produces
a numbered human-readable breakdown, a DataGenerator generation Pattern, a whole-value .NET Regex and a SQL Server
predicate. It never executes the SQL.

## Feed syntax

This is explicit segment syntax, not automatic inference from arbitrary examples. In particular, digits outside brackets
are literal digits, whereas a single run of digits inside brackets specifies **width**, not a literal value.

| Feed part | Meaning |
| --- | --- |
| `MS` or `-Y` | Always exactly that literal text, case-sensitive. |
| `[10]` | Any **two** ASCII digits, from `00` to `99`. `10` specifies the width; it is not a count of ten digits. |
| `[1]` | Any one ASCII digit, from `0` to `9`. |
| `[01, 03, 05]` | Exactly one of those three two-digit values, preserving leading zeroes. |
| `[LR]` | Exactly one character: `L` or `R`. Alphabetic choice sets are case-sensitive. |
| `[001 > 050]` | Exactly three ASCII digits, strictly greater than `50`: `051` through `999`. The left side specifies width, **not** a lower bound. |
| `\[` / `\]` / `\\` | A literal bracket or backslash outside a segment. |

Only nonblank, printable ASCII feed text is supported, with a maximum of 1,024 characters and numeric segment widths of 1-9.
Numeric lists must have equal-width entries without duplicates. Thresholds must have the same width as their template
and leave at least one possible value. Spaces around comma entries and `>` are ignored; spaces outside brackets are
literal. Empty/nested/unclosed brackets, unsupported operators such as `<`, `>=` and ranges, mixed letter/numeric
lists, duplicate alphabetic choices and invalid escapes are rejected with an explanation. No guessed fallback is used.

## Worked example

Feed:

```text
MS[10]-Y[01, 03, 05]-X[001 > 050]-[LR]-D[1]
```

This always starts with `MS`, then two digits (`00`-`99`), then `-Y`, then exactly `01`, `03` or `05`, then `-X`,
then three digits greater than `50`, then `-`, then `L` or `R`, then `-D`, then one digit (`0`-`9`).
For example, `MS00-Y03-X051-L-D0` matches; `MS00-Y03-X050-L-D0` does not.

The Pattern output can be pasted into a column's Pattern editor:

```text
'MS' FOLLOWED BY RAND_NUM(0, 99, 2) FOLLOWED BY '-Y' FOLLOWED BY ONE_OF('01', '03', '05') FOLLOWED BY '-X' FOLLOWED BY RAND_NUM(51, 999, 3) FOLLOWED BY '-' FOLLOWED BY ONE_OF('L', 'R') FOLLOWED BY '-D' FOLLOWED BY RAND_NUM(0, 9, 1)
```

The Regex uses ASCII `[0-9]`, .NET whole-string anchors `\A` and `\z` and bounded alternatives for numeric thresholds.
It is a **matcher** for .NET code, not a SQL regex engine or JavaScript. The application's built-in Regex **generation**
mode supports only a smaller subset and cannot expand these anchored alternatives; use the generated **Pattern** for
data generation instead. SQL filtering can use the generated SQL predicate without database regex support.

The SQL output is a predicate to paste after `WHERE`, not an executable query. **SQL column name** is one identifier
(default `Value`), not SQL code or a qualified `table.column` reference; the builder quotes it with brackets and escapes
closing brackets. Literals are Unicode SQL string constants with escaped quotes. The predicate converts the column
to `nvarchar(max)`, checks exact byte length (including trailing spaces), and uses binary collation
`Latin1_General_100_BIN2`, `SUBSTRING`, `LIKE` and `TRY_CONVERT(int, ...)`. Numeric checks use `TRY_CONVERT` so optimizer
reordering cannot cause conversion failures for nonnumeric rows. SQL Server 2012 or later is required. NULL values
do not match. Generated SQL does not depend on database regex support.

For matching codes, use a text column: numeric columns have already lost leading zeroes. Review the column name and
predicate in the intended query before use; this window does not connect to a database or run a query.

## Save, find and reuse

**Build and save** stores the entered feed, SQL column name and all four outputs. Give the entry a memorable optional
**Saved name**; otherwise the feed text becomes its name. An existing name is replaced only after confirmation.
**Save as named entry** saves the current outputs under the edited name without rebuilding. Editing the feed or SQL
column clears stale outputs until you build again.

Search saved names or feeds in the left-hand panel. Select an entry to recall its original input and every saved output;
use the individual **Copy** buttons or select text in the read-only output boxes. Delete an entry only after confirmation.
The saved list is newest first and includes timestamps.

The library is indented, versioned JSON at:

```text
%LOCALAPPDATA%\LocalTools\DataGenerator\SavedExpressions.json
```

Writes use a temporary file followed by replacement, just like other application libraries. Save failures are reported
and do not update the in-memory saved list. If the library cannot be read, the window reports the error and disables
saving/deletion rather than overwriting it; building and copying still work. Repair or restore that file and reopen the
window to re-enable saving. The library stores text only; recalled SQL and other outputs are never automatically executed.
