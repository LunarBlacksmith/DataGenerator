# DataGenerator saved column settings

A saved setting remembers how a column is generated (its **Generation mode** and the values in its **Settings** cell) under a name, so that it can be used again for other columns, other row sets and later sessions.
Saved settings can be exported to a file and imported by colleagues, so that everybody generating test data for the same project follows the same rules.

To save or load the settings of **every column of a row set at once**, use a [set configuration](#set-configurations) instead.
Saved column settings and set configurations are kept apart, in different menus and different files, so loading one can never be mistaken for the other.

## Contents

- [Saving a column's settings](#saving-a-columns-settings)
- [Using a saved setting](#using-a-saved-setting)
- [Applying settings automatically](#applying-settings-automatically)
- [Managing saved settings](#managing-saved-settings)
- [Sharing settings with colleagues](#sharing-settings-with-colleagues)
- [Where the settings are kept](#where-the-settings-are-kept)
- [File format](#file-format)
- [Set configurations](#set-configurations)

## Saving a column's settings

1. In the **Column rules** grid, set up the column: choose its **Generation mode** and fill in its **Settings** cell.
2. Click the saved-settings button at the right of the **Settings** cell (the star) and choose **Save these settings…**.
3. Give the setting a name. Using an existing name replaces that saved setting; the dialog says so before you save.
4. Choose whether the setting is applied automatically (see [Applying settings automatically](#applying-settings-automatically)) and click **Save**.

Only the values used by the chosen mode are saved, for example the pattern of a **Pattern** column or the start and step of a **Sequence** column.
Settings with mistakes (outlined in red) cannot be saved.
Columns whose values are always chosen by SQL Server, such as identity columns, have no saved-settings button.

## Using a saved setting

Click the saved-settings button of any column and pick a setting under **Apply a saved setting**.
The menu lists only the settings whose generation mode suits the column's SQL type; settings saved for a column with the same name are listed first and marked *saved for this column*.

While a column uses a saved setting, its saved-settings button is highlighted and its hint shows the setting's name.
Changing the column's mode or settings by hand afterwards ends the link, and the button is no longer highlighted.
Renaming the saved setting in the manager keeps the link; deleting it ends the link but leaves the column's values as they are.

## Applying settings automatically

A saved setting can be applied automatically to:

| Option                                  | Applies to                                                         |
| --------------------------------------- | ------------------------------------------------------------------ |
| **Never**                               | Nothing; the setting is used only when picked from a column's menu. |
| **Only for *column* in *table***        | That column of that table only.                                    |
| **For every column named *column***     | Every column with that name, in any table and any database.        |

Automatic settings are used whenever new column rules are created: when a table's columns are first shown and when a row set is added.
A setting saved for an exact table and column wins over a setting for the column name in any table.
Each target has at most one automatic setting; saving another automatic setting for the same target turns the old one off (the dialog warns about this).

Row sets that already exist are not changed when an automatic setting is saved. To update them, use **Apply automatic settings to open tables** in the manager.
It replaces the settings of every matching column in every open row set, including columns you have changed by hand, so it asks first.

## Managing saved settings

Open the manager with the **Saved column settings** button below the **Column rules** grid, or with **Manage saved settings…** in any column's saved-settings menu.

| Control                                   | What it does                                                                                        |
| ----------------------------------------- | --------------------------------------------------------------------------------------------------- |
| **Name**                                  | Type a new name to rename the setting. Columns using it follow the new name.                        |
| **Mode** and **Settings**                 | The saved generation mode and values.                                                               |
| **Automatic**                             | Tick to apply the setting automatically to its target. Settings saved with **Never** keep their table and column, so they can be ticked later. |
| **Applies to**                            | The table and column, or the column name, the setting is saved for.                                 |
| **Delete**                                | Removes the selected setting.                                                                       |
| **Import…**                               | Adds the settings of a file. When names already exist you choose between replacing your settings and importing only the new ones. |
| **Export…**                               | Writes all saved settings to a file.                                                                |
| **Apply automatic settings to open tables** | Applies the automatic settings to the row sets that are already open.                             |

Every change is saved to disk immediately. If a change cannot be saved, it is undone and the reason is shown.

## Sharing settings with colleagues

1. Open the manager and click **Export…** to write your saved settings to a file.
2. Send the file to your colleagues, or keep it in the project's source control.
3. Each colleague opens the manager, clicks **Import…** and chooses the file.
4. Imported automatic settings are used for new row sets straight away; click **Apply automatic settings to open tables** to update the open ones.

## Where the settings are kept

The settings are kept per Windows user in:

```
%LOCALAPPDATA%\LocalTools\DataGenerator\SavedColumnSettings.json
```

The manager shows the full path at the bottom of its window.
If the file cannot be read when the app starts (for example after editing it by hand), the app starts with an empty list, renames the file to `SavedColumnSettings.unreadable-<date>-<time>.json` so it is not overwritten, and tells you where it was kept.

## File format

Saved and exported files are indented JSON, so they can be reviewed and compared in source control:

```json
{
  "formatVersion": 1,
  "settings": [
    {
      "name": "Shirt codes",
      "generationMode": "Pattern",
      "fixedValue": "",
      "sequenceStart": "1",
      "sequenceStep": "1",
      "regexPattern": "",
      "patternExpression": "'P' + SEQ(1, 1000, 1) + ('X' OR 'Y')",
      "sourceColumnName": "",
      "tableName": null,
      "columnName": "Code",
      "applyAutomatically": true
    }
  ]
}
```

| Property             | Meaning                                                                                                   |
| -------------------- | --------------------------------------------------------------------------------------------------------- |
| `name`               | The setting's name (at most 100 characters, unique regardless of casing).                                 |
| `generationMode`     | The generation mode, for example `Random`, `Fixed`, `Sequence`, `Regex`, `Pattern`, `CopyColumn` or `Null`. |
| `fixedValue`         | The value of a **Fixed** column.                                                                          |
| `sequenceStart`, `sequenceStep` | The start and step of a **Sequence** column.                                                   |
| `regexPattern`       | The regular expression of a **Regex** column.                                                             |
| `patternExpression`  | The pattern of a **Pattern** column; see [the pattern language](PatternLanguage.md).                       |
| `sourceColumnName`   | The column copied by a **Copy of column** column.                                                         |
| `tableName`          | The table (`schema.table`) the setting was saved for, or `null` for every column with the name.           |
| `columnName`         | The column the setting was saved for.                                                                     |
| `applyAutomatically` | Whether the setting is applied automatically to its target.                                               |

## Set configurations

A set configuration remembers the **number of rows**, **Generation mode** and **Settings** of every column of a row set under one name.
Use it when a whole row set should be generated the same way again, in a later session, in another row set or by a colleague.

| Saved column setting                                        | Set configuration                                                   |
| ----------------------------------------------------------- | ------------------------------------------------------------------- |
| One column                                                  | Row count and every column of a row set                             |
| Saved and applied with the star button in a **Settings** cell | Saved and loaded with **Set configuration** above the grid        |
| Can be applied automatically                                | Only loaded when you pick it                                        |
| `SavedColumnSettings.json`                                  | `SavedSetConfigurations.json`                                       |

### Saving a set configuration

1. Set up the columns and number of rows of the row set.
2. Click **Set configuration** above the grid. **Save as** is filled in with the table and row set name. Type a new name, or open its dropdown and select any saved configuration to overwrite (including one from another table).
3. Click **Save** or press Enter. Using an existing name asks before replacing it.

The number of rows is saved along with the columns. The row set's name is not restored when loading a configuration.
Columns whose values SQL Server always chooses, such as identity columns, are left out; they are never changed by loading a configuration either.

### Loading a set configuration

Click **Set configuration** in the row set to change, and click a configuration under **Load into this set**.
The scrollable list has subtle dividers between configurations to make long lists easier to scan.
The app says how many columns will change and which row count will be restored, and asks first. Then:

- The saved row count is restored. Older configurations without a row count keep the current set's count.
- Columns are matched **by name** (casing is ignored). Each matching column gets the saved mode and settings, replacing its own.
- Columns the configuration does not have keep their settings.
- A setting that does not suit a column (for example text in a **Fixed** value of an `int` column, or **NULL** for a column that does not allow it) is not applied; the column keeps its settings.
- A short report appears next to the button. Hover over it to see which columns were left unchanged and why. Click × to hide it.

Configurations saved from the same table are listed first. Configurations from other tables are listed if they share at least one column name, so a configuration can be reused for similar tables.
Columns that use a saved column setting lose that link when a configuration is loaded, because their settings now come from the configuration.

### Sharing set configurations

Use **Import set configurations…** and **Export all set configurations…** at the bottom of the **Set configuration** menu.
When imported names already exist you choose between replacing yours and importing only the new ones.
Exported files can only be imported as set configurations; a file of saved column settings is refused by the set configuration import, and the other way round.

The configurations are kept next to the saved column settings in:

```
%LOCALAPPDATA%\LocalTools\DataGenerator\SavedSetConfigurations.json
```

If the file cannot be read when the app starts, it is renamed to `SavedSetConfigurations.unreadable-<date>-<time>.json` and the app says where it was kept.

```json
{
  "formatVersion": 2,
  "setConfigurations": [
    {
      "name": "dbo.Shirt – Large shirts",
      "tableName": "dbo.Shirt",
      "savedAt": "2026-10-01T09:30:00+01:00",
      "rowCount": 10,
      "columns": [
        {
          "name": "Size",
          "generationMode": "Fixed",
          "fixedValue": "XL",
          "columnName": "Size"
        }
      ]
    }
  ]
}
```

Each entry of `columns` has the same properties as a saved column setting (see [File format](#file-format)); `columnName` is the column it is loaded into.

`rowCount` must be between 1 and 1,000,000. Files from format version 1 remain readable; an absent or null count means
the current row count is left unchanged. New exports use version 2 so older app versions do not silently ignore the saved count.
