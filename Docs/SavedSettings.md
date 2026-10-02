# DataGenerator saved column settings

A saved setting remembers how a column is generated (its **Generation mode** and the values in its **Settings** cell) under a name, so that it can be used again for other columns, other row sets and later sessions.
Saved settings can be exported to a file and imported by colleagues, so that everybody generating test data for the same project follows the same rules.

## Contents

- [Saving a column's settings](#saving-a-columns-settings)
- [Using a saved setting](#using-a-saved-setting)
- [Applying settings automatically](#applying-settings-automatically)
- [Managing saved settings](#managing-saved-settings)
- [Sharing settings with colleagues](#sharing-settings-with-colleagues)
- [Where the settings are kept](#where-the-settings-are-kept)
- [File format](#file-format)

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

Open the manager with the **Saved settings** button below the **Column rules** grid, or with **Manage saved settings…** in any column's saved-settings menu.

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
| `generationMode`     | The generation mode, for example `Random`, `Fixed`, `Sequence`, `Regex`, `Pattern` or `Null`.             |
| `fixedValue`         | The value of a **Fixed** column.                                                                          |
| `sequenceStart`, `sequenceStep` | The start and step of a **Sequence** column.                                                   |
| `regexPattern`       | The regular expression of a **Regex** column.                                                             |
| `patternExpression`  | The pattern of a **Pattern** column; see [the pattern language](PatternLanguage.md).                       |
| `tableName`          | The table (`schema.table`) the setting was saved for, or `null` for every column with the name.           |
| `columnName`         | The column the setting was saved for.                                                                     |
| `applyAutomatically` | Whether the setting is applied automatically to its target.                                               |