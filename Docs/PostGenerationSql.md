# DataGenerator post-generation SQL

The **Finally run** box in the **Generation output** section holds stored procedures or SQL that run after all the generated rows are inserted, in the same transaction and just before it is committed.
Use it to recalculate totals, refresh summary tables, fix up data the generator cannot produce, or check the generated data and raise an error when it is wrong.

## Contents

- [How it runs](#how-it-runs)
- [Stored procedures](#stored-procedures)
- [SQL](#sql)
- [Mixing stored procedures and SQL](#mixing-stored-procedures-and-sql)
- [The database it runs in](#the-database-it-runs-in)
- [Errors](#errors)
- [Examples](#examples)

## How it runs

- The statements run in the order they are typed, after the cleanup (when **Clear existing data first** is ticked) and after every insert.
- They run inside the generation's transaction, so if any of them fails, the whole generation is rolled back and nothing is saved.
- In **SQL file** mode they are written at the end of the script, just before `COMMIT TRANSACTION`, each with a comment showing the line it came from.
- In **Direct insert** mode they are executed against the server, just before the commit, with a timeout of 10 minutes each.
- The box is optional: leave it empty to run nothing.

The summary under the box shows what will run, for example *Runs 1 stored procedure and 1 block of SQL in [Retail] after the inserts, before the commit.*
When the text cannot be run, the summary shows the problem in the warning colour and **Generate** is disabled until it is fixed.

## Stored procedures

Type the name of a stored procedure on a line of its own; `EXEC` is added for you.
Put each stored procedure on its own line.

```
dbo.RebuildTotals
[Sales].[Refresh Summary];
usp_RecalculateStock -- a comment after the name is allowed
```

The name can have up to four parts (`server.database.schema.procedure`), each a regular name or a `[bracketed]` one, and may end with a semicolon.
To pass parameters, write the `EXEC` statement yourself, for example `EXEC dbo.RebuildTotals @FromDate = '2024-01-01';`.

## SQL

Every line that is not a stored procedure name is SQL, and it runs exactly as typed.
Consecutive SQL lines run together, so a statement can span several lines.

```
UPDATE dbo.Shirt
SET    Price = 9.99
WHERE  Price IS NULL;
```

Single words that are part of SQL, such as `BEGIN`, `END` or `COMMIT`, are never taken for stored procedure names.
`--` comments can be used anywhere; lines that hold only comments do nothing.

`GO` cannot be used, because everything runs in one transaction; end each statement with a semicolon instead.
Statements that must be the first in a batch, such as `CREATE PROCEDURE`, cannot be used either.
Do not commit or roll back the transaction yourself.

## Mixing stored procedures and SQL

A name on a line after SQL is only treated as a stored procedure when the SQL before it ends with a semicolon or is followed by a blank line.
Otherwise the name is taken as the continuation of that SQL, for example the second line of:

```
SELECT *
FROM dbo.Shirt
```

So end SQL with a semicolon (or leave a blank line) before the next stored procedure:

```
UPDATE dbo.Shirt SET Price = 9.99 WHERE Price IS NULL;
dbo.RebuildTotals
```

## The database it runs in

The **in database** box chooses the database the statements run in: the script switches to it with `USE` first, so names without a database, such as `dbo.RebuildTotals`, are found there.
Until a database is chosen, it follows the database of the included tables.
Because of the `USE`, a SQL script stays in that database after it finishes.

## Errors

When a statement fails during a direct insert, the error says which line of the box it came from, for example *Post-generation SQL › database [Retail] › Line 3: stored procedure dbo.RebuildTotals*, and nothing is saved.
When a script fails, SQL Server reports the error and the script rolls everything back.

## Examples

Recalculate totals and check the result:

```
dbo.RebuildTotals

IF EXISTS (SELECT 1 FROM dbo.Shirt WHERE Price < 0)
   THROW 50000, 'Generated shirts must not have negative prices.', 1;
```

Run several stored procedures in order:

```
dbo.RefreshStock
dbo.RefreshPrices
[Reporting].[Rebuild Daily Summary]
```