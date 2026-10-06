using DataGenerator.Infrastructure;

namespace DataGenerator.Models;

public sealed class ColumnModel : ObservableObject
{
	#region FIELDS
	#region PUBLIC
	public const string FOREIGN_TABLE_KEY_SUFFIX = "ftk";
	#endregion PUBLIC

	#region PRIVATE
	private string _name;
	private string _sqlType;
	private int?   _maximumLength;
	private byte?  _precision;
	private byte?  _scale;
	private bool   _isNullable;
	private bool   _isIdentity;
	private bool   _isComputed;
	private bool   _isPrimaryKey;
	private bool   _isForeignKey;
	private bool   _hasDefault;
	#endregion PRIVATE
	#endregion FIELDS

	#region PROPERTIES
	#region PUBLIC
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
	///	Columns whose name ends with "ftk" (any casing) are treated as foreign table keys by naming convention,
	///	even when SQL Server has no foreign key constraint for them.
	/// </summary>
	public bool IsForeignTableKey
		=> Name.Length > FOREIGN_TABLE_KEY_SUFFIX.Length
			&& Name.EndsWith(FOREIGN_TABLE_KEY_SUFFIX, StringComparison.OrdinalIgnoreCase);
	#endregion PUBLIC
	#endregion PROPERTIES

	#region CONSTRUCTORS
	#region PUBLIC
	/// <summary>
	///	Creates a new <see cref="ColumnModel"/> and sets the default values of its fields and properties.
	/// </summary>
	public ColumnModel()
	{
		_name          = string.Empty;
		_sqlType       = string.Empty;
		_maximumLength = null;
		_precision     = null;
		_scale         = null;
		_isNullable    = false;
		_isIdentity    = false;
		_isComputed    = false;
		_isPrimaryKey  = false;
		_isForeignKey  = false;
		_hasDefault    = false;
	}
	#endregion PUBLIC
	#endregion CONSTRUCTORS
}