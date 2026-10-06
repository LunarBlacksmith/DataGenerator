using DataGenerator.Infrastructure;

namespace DataGenerator.Models;

public sealed class ColumnModel : ObservableObject
{
	public const string FOREIGN_TABLE_KEY_SUFFIX = "ftk";

	private string _name          = string.Empty;
	private string _sqlType       = string.Empty;
	private int?   _maximumLength = null;
	private byte?  _precision     = null;
	private byte?  _scale         = null;
	private bool   _isNullable    = false;
	private bool   _isIdentity    = false;
	private bool   _isComputed    = false;
	private bool   _isPrimaryKey  = false;
	private bool   _isForeignKey  = false;
	private bool   _hasDefault    = false;

	public string Name
	{
		get => _name;
		set
		{
			if (SetProperty(ref _name, value))
			{
				OnPropertyChanged(nameof(IsForeignTableKey));
			}
		}
	}

	public string SqlType
	{
		get => _sqlType;
		set => SetProperty(ref _sqlType, value);
	}

	public int? MaximumLength
	{
		get => _maximumLength;
		set => SetProperty(ref _maximumLength, value);
	}

	public byte? Precision
	{
		get => _precision;
		set => SetProperty(ref _precision, value);
	}

	public byte? Scale
	{
		get => _scale;
		set => SetProperty(ref _scale, value);
	}

	public bool IsNullable
	{
		get => _isNullable;
		set => SetProperty(ref _isNullable, value);
	}

	public bool IsIdentity
	{
		get => _isIdentity;
		set => SetProperty(ref _isIdentity, value);
	}

	public bool IsComputed
	{
		get => _isComputed;
		set => SetProperty(ref _isComputed, value);
	}

	public bool IsPrimaryKey
	{
		get => _isPrimaryKey;
		set => SetProperty(ref _isPrimaryKey, value);
	}

	public bool IsForeignKey
	{
		get => _isForeignKey;
		set => SetProperty(ref _isForeignKey, value);
	}

	public bool HasDefault
	{
		get => _hasDefault;
		set => SetProperty(ref _hasDefault, value);
	}

	/// <summary>
	/// Columns whose name ends with "ftk" (any casing) are treated as foreign table keys by naming convention,
	/// even when SQL Server has no foreign key constraint for them.
	/// </summary>
	public bool IsForeignTableKey
		=> Name.Length > FOREIGN_TABLE_KEY_SUFFIX.Length
			&& Name.EndsWith(FOREIGN_TABLE_KEY_SUFFIX, StringComparison.OrdinalIgnoreCase);
}