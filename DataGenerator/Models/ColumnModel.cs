using DataGenerator.Infrastructure;

namespace DataGenerator.Models;

public sealed class ColumnModel : ObservableObject
{
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
	private string _fixedValue    = string.Empty;
	private long   _sequenceStart = 1;
	private long   _sequenceStep  = 1;
	private string _regexPattern  = string.Empty;
	private ValueGenerationMode _generationMode = ValueGenerationMode.Random;

	public string Name
	{
		get => _name;
		set => SetProperty(ref _name, value);
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

	public ValueGenerationMode GenerationMode
	{
		get => _generationMode;
		set => SetProperty(ref _generationMode, value);
	}

	public string FixedValue
	{
		get => _fixedValue;
		set => SetProperty(ref _fixedValue, value);
	}

	public long SequenceStart
	{
		get => _sequenceStart;
		set => SetProperty(ref _sequenceStart, value);
	}

	public long SequenceStep
	{
		get => _sequenceStep;
		set => SetProperty(ref _sequenceStep, value);
	}

	public string RegexPattern
	{
		get => _regexPattern;
		set => SetProperty(ref _regexPattern, value);
	}
}