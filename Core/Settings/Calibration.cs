using System;
using System.Text.Json.Serialization;

using Godot;

using RoverControlApp.Core.JSONConverters;

namespace RoverControlApp.Core.Settings;

[JsonConverter(typeof(CalibrationConverter))]
public partial class Calibration : SettingBase, ICloneable
{

	public Calibration()
	{
		_calibrationMotor = new();
		_msgLimiter = 100;
	}

	public Calibration(CalibrationMotor calibrationMotor, int msgLimiter)
	{
		_calibrationMotor = calibrationMotor;
		_msgLimiter = msgLimiter;
	}

	public object Clone()
	{
		return new Calibration()
		{
			CalibrationMotor = _calibrationMotor,
			MsgLimiter = _msgLimiter
		};
	}

	[SettingsManagerVisible(cellMode: TreeItem.TreeCellMode.Custom, immutableSection: true)]
	public CalibrationMotor CalibrationMotor
	{
		get => _calibrationMotor;
		set => EmitSignal_SectionChanged(ref _calibrationMotor, value);
	}

	[SettingsManagerVisible(cellMode: TreeItem.TreeCellMode.Range, formatData: "100;1000;10;f;i", customName: "Message Limiter", customTooltip: "How long to wait before sending another message")]
	public int MsgLimiter
	{
		get => _msgLimiter;
		set => EmitSignal_SectionChanged(ref _msgLimiter, value);
	}

	CalibrationMotor _calibrationMotor;
	int _msgLimiter;
}
