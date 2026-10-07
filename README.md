# DataGenerator

A Windows desktop tool for generating realistic SQL Server test data in large quantities. You decide which tables to fill, how many rows each table gets, and how every column's values are produced. DataGenerator either inserts the rows straight into the databases or writes a SQL script you can review and run, and it keeps foreign keys, data types and constraints consistent.

> DataGenerator is meant for **non-production and test databases only**. Before a direct insert, the app asks you to confirm that the databases are authorised test databases.

## Contents

- [Features](#features)
- [Requirements](#requirements)
- [Building and running](#building-and-running)
- [Quick start](#quick-start)
- [Value generation modes](#value-generation-modes)
- [Documentation](#documentation)
- [Where files are stored](#where-files-are-stored)
- [Project structure](#project-structure)
- [Architecture](#architecture)
- [Coding conventions](#coding-conventions)

## Features

- **Database explorer tree.** Every database on the server is shown, with its tables underneath.
  - Hide databases you are not working on with the eye button. A hidden database collapses and turns grey.
  - Multi-select databases and tables to include or exclude them, expand or collapse them, hide them, or set their row counts in bulk.
  - Primary key tables, foreign key tables and FTK tables are told apart by colour.
- **Column rules grid.** One row per column, showing its type, keys, nullability, generation mode, settings and live sample values.
  - The **Settings** cell shows only the input the chosen mode needs, and checks it against the column's SQL type as you type.
  - Primary key rows are pastel red. Foreign key rows, and FTK rows (columns ending in `FTK`), are pastel orange.
  - Select several columns to change their mode or settings in bulk. A column that cannot take the change keeps its current rule.
  - Show or hide grid columns such as **Keys** or **Nullable**; the choice is remembered.
  - Move between **Settings** cells with the arrow keys.
- **Row sets.** A table can have several insert sets, each with its own row count and column rules. For example, 10 shirts with one set of values and 10 more with completely different values. Drag a Set tab to move it before or after another tab, or Shift+drag to swap two tabs; overflowing tabs scroll horizontally with the mouse wheel, and the strip also scrolls while dragging near its ends or turning the wheel.
- **Steps and update sets.**
  - Runs are split into numbered steps that execute in order.
  - **Update sets** change rows that are already there or that an earlier step generated. You choose how many rows, which rows (`WHERE` condition) and the new column values.
  - Everything runs in one transaction. If anything fails, nothing is changed and no SQL file is written.
- **Pattern language.** Describe values in plain English. For example:

  ```
  P FOLLOWED BY SEQ(1-1000, 1) FOLLOWED BY (X OR Y) FOLLOWED BY (RAND_NUM(0, 99, 2))
  ```

  produces `P0001Y73`, `P0002X04`, … It also offers parity-limited numbers (`ODD(1, 99)`, `EVEN(0, 100)`), dates (`TODAY()`, `RAND_DATE()`), text functions, `FIRST`/`LAST`, values of other columns (`COL(Name)`) and functions inside functions. The editor has non-intrusive completion: press <kbd>Tab</kbd> twice to accept a suggestion.
- **Value from table.** Use values of a column of any loaded table, taken from existing rows and/or rows generated in the same run. They can be unique, and filtered by a pattern, a regular expression or a SQL condition.
- **Copy of column.** Copy another column of the same row, converted automatically to this column's type.
- **Foreign table keys (FTK).** Columns ending in `FTK` are treated like foreign keys even when SQL Server has no constraint for them. DataGenerator offers to generate rows for the table they point to, found by its `…PK`, `…TK` or `…_tk` column.
- **Saved settings and set configurations.**
  - Save a single column's settings, or a whole set's row count and column rules, and load them later or share them with colleagues as JSON files.
  - Column settings and set configurations are kept and loaded separately, so loading one never overwrites the other by surprise.
- **Clear existing data first.** Optionally empty the included tables, or every table in their databases, before inserting, and reseed identities. This works for direct inserts and SQL scripts.
- **Post-generation SQL.** Run stored procedures or any SQL at the end of the run, inside the same transaction.
- **Output.** Insert directly in one transaction, or write a `.sql` script. Open the output folder from the app.
- **Quality of life.**
  - Every control has a hint. Hints open beside the mouse cursor and close by themselves after a time that suits their length.
  - Light and dark themes.
  - Error messages show where the problem happened, in a scrollable panel.
  - Pressing <kbd>Enter</kbd> in the password box loads the metadata.

## Requirements

- Windows 10 or later.
- The [.NET 10 SDK](https://dotnet.microsoft.com/download) to build it, or the .NET 10 Desktop Runtime to run a published build.
- SQL Server reachable with SQL Server authentication: a user name and password. For **Value from table** filters with `WHERE REGEX`, you need SQL Server 2025 with database compatibility level 170.

## Building and running

From the repository root:

```powershell
dotnet build DataGenerator.slnx
dotnet run --project DataGenerator
```

The build treats warnings as errors.

## Quick start

1. **Connect.** Enter the server, user name and password. **Trust server certificate** is ticked by default. Press <kbd>Enter</kbd> or click **Load metadata**.
2. **Choose tables.** In the explorer tree, tick the tables to fill and set how many rows each one gets. Hide the databases you don't need with the eye button.
3. **Set the column rules.** Select a table to see its columns. For each column, choose a **Generation mode** and fill in its **Settings**. The **Sample values** column previews the result. Add more sets with **Add row set** or **Add update set**.
4. **Choose the output.**
   - Choose **SQL file** or **Direct insert**. A direct insert needs you to confirm that the databases are test databases.
   - Optionally clear existing data first.
   - Optionally add stored procedures or SQL to run at the end.
5. **Generate.** Click **Generate**. If tables you have not included are needed, for example by **Generated key** or FTK columns, the app asks whether to include them.

## Value generation modes

| Mode                   | Values                                                                                                   |
| ---------------------- | -------------------------------------------------------------------------------------------------------- |
| **Random**             | A random value that suits the column's SQL type.                                                         |
| **Fixed value**        | The same value in every row.                                                                             |
| **Sequence**           | Counts from a start value by a step. Restarts for every row set.                                         |
| **Pattern**            | Built from a [pattern](Docs/PatternLanguage.md).                                                         |
| **Regex**              | Random text matching a simple regular expression, e.g. `[A-Z]{3}-[0-9]{4}`.                              |
| **Copy of column**     | Another column of the same row, converted to this column's type.                                         |
| **Value from table**   | A value of a column of a loaded table, existing or generated in this run.                                |
| **Generated key**      | A key of a row generated for the referenced table in the same run.                                       |
| **Existing key**       | A key that already exists in the referenced table.                                                       |
| **Database generated** | Left out of the `INSERT` so SQL Server supplies it (identity, computed, `rowversion` or default).         |
| **NULL**               | `NULL` in every row (nullable columns only).                                                             |
| **Keep current value** | Update sets only: the column is not changed.                                                             |

## Documentation

Press **F1** or click the small documentation icon in the status bar to read the bundled guides offline inside the app.
The reader shows the guides as formatted pages (headings, tables, code and keyboard keys) in the current light or dark theme, starting with the SQL WHERE, lookup and execution guide. Links move between guides, Ctrl+F finds text and Ctrl+wheel zooms. It uses the Microsoft Edge WebView2 Runtime, which is included with Windows 10 and 11; without it the reader shows the plain Markdown text.

Anywhere in the app, **Shift + mouse wheel** scrolls wide content sideways, such as the column rules grid and long sample values.

| Document                                                                   | Covers                                                                                       |
| -------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| [Pattern language](Docs/PatternLanguage.md)                                | Every pattern function, operators, column types, row numbering, limits, errors and grammar.  |
| [Steps, update sets and values from tables](Docs/StepsUpdatesAndLookups.md) | Steps, update sets, `Value from table`, SQL conditions (`s.` and `t.`), FTK columns.        |
| [Saved settings and set configurations](Docs/SavedSettings.md)             | Saving, applying, managing and sharing column settings and set configurations.               |
| [Post-generation SQL](Docs/PostGenerationSql.md)                           | Running stored procedures or SQL at the end of a run.                                        |

## Where files are stored

Everything is kept per Windows user under `%LOCALAPPDATA%\LocalTools\DataGenerator\`:

| Path                           | Contents                                                       |
| ------------------------------ | -------------------------------------------------------------- |
| `GeneratedData\`               | Generated SQL scripts (default output folder).                 |
| `SavedColumnSettings.json`     | Saved column settings.                                         |
| `SavedSetConfigurations.json`  | Saved set configurations.                                      |
| `Preferences.json`             | Theme and hidden grid columns.                                 |

Passwords are never saved.

## Project structure

```
DataGenerator.slnx
Docs\                      User documentation (linked above)
DataGenerator\
   App.xaml(.cs)           Composition root: creates the services and view models, applies the theme
   MainWindow.xaml(.cs)    Main window layout
   Infrastructure\         MVVM plumbing: ObservableObject, commands, converters, attached behaviours, hints
   Interfaces\             Service abstractions (I…)
   Models\                 Plain data types: metadata, column rules, row sets, settings, results
   Services\               Metadata loading, value generation, saved settings, themes, dialogs, file paths
      Generation\          Blueprints, step ordering, SQL script writer, direct inserter, key and lookup pools
      Patterns\            Pattern language lexer, parser, function nodes and matchers
   ViewModels\             View models for the connection, explorer, column rules, sets, output and windows
   Views\                  User controls and windows (XAML), with minimal code-behind
   Themes\                 Light and dark brushes, control styles and hint texts
```

## Architecture

- **MVVM.**
  - Views bind to view models. Code-behind holds only view concerns, such as focus, keyboard navigation and popups.
  - View models expose state and `ICommand`s, and never reference WPF controls.
  - Models are plain data.
- **Dependency injection by hand.** `App.xaml.cs` is the single composition root. Services are created there and passed to view models through their constructors as interfaces from `Interfaces\`.
- **SOLID.**
  - Each service has one job, for example parsing patterns, writing scripts, inserting rows or storing preferences.
  - New pattern functions and generation modes are added as new types rather than by editing large switch statements where possible.
- **Themes.** Colours are `DynamicResource` brushes in `Themes\Brushes.Light.xaml` and `Themes\Brushes.Dark.xaml`. `ThemeService` swaps them at run time.

## Coding conventions

- Tabs for indentation. Alignment assumes a tab width of 3.
- `static` and `const` names in `UPPER_SNAKE_CASE`.
- Explicit types: no `var`.
- Target-typed `new()` and collection expressions (`[]`). Interface-typed declarations use `new ExplicitType()`.
- Prefix increments and decrements (`++i`, `--i`).
- Curly braces on every body, including single-line `if` statements and `switch` cases.
- Multi-line conditional expressions put the condition on its own line, with `?` and `:` indented one level further. Nested conditionals indent another level.
- Short `if (...) { return ...; }` guards followed by a final `return` are written as one waterfall conditional expression. A method whose body is a single `return` uses an expression body (`=>`).
- LINQ statements with more than two `.` characters (counted over the whole statement, or over a lambda's expression body) put the receiver on its own line and every `.Call` (including the first) on its own line, one level deeper. When a link's arguments do not fit on one line (100 columns), each argument goes on its own line and the closing `)` lines up with the link. A chain that is only an operand inside a larger expression (for example `!items.Any(...)`) is moved into a well-named local first.

  ```csharp
  bool referencedColumnExists =
  	referencedPlan
  		.Table
  		.Columns
  		.Any(
  			item => string.Equals(
  				item.Name,
  				reference.ReferencedColumn,
  				StringComparison.OrdinalIgnoreCase
  			)
  		);
  ```
- Multi-line collection expressions are laid out like method calls: `[..` (or `[`) stays on the line of the assignment, and the closing `]` is on its own line at the assignment's indentation.

  ```csharp
  Tables = [..
  	model
  		.Tables
  		.OrderBy(table => table.SchemaName, StringComparer.OrdinalIgnoreCase)
  		.Select(table => new TableNodeViewModel(table, this, ruleFactory))
  ];
  ```
- Every method has an XML documentation comment, with `<summary>`, `<param>`, `<returns>` and `<exception>` tags as needed. Tag lines use `/// `, text lines use `///` followed by a tab, and `<para>` blocks separate longer summaries.
- Class layout, top to bottom: fields, properties, events, constructors, methods, nested types. Each is wrapped in a named region (`#region FIELDS` … `#endregion FIELDS`), with nested accessor regions (`#region PUBLIC`, `#region PRIVATE`, and so on), and `static`/`const` members at the top of their region. Classes that hold only fields or properties still use the regions.
- Every class declares a constructor (static classes declare a static constructor). Default values for fields and properties are assigned in the constructor, never in an initialiser on the declaration. `const` values are the exception, because the language requires an initialiser. Static members are assigned in the static constructor.
