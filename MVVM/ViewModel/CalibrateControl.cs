using Godot;
using RoverControlApp.Core;
using RoverControlApp.MVVM.Model;
using System;
using System.Threading.Tasks;

namespace RoverControlApp.MVVM.ViewModel;
public partial class CalibrateControl : Panel
{
	private enum HookAction { Enter, Exit }

	[ExportGroup("Axis")]
	[Export] private Sprite2D[] AxisModels = new Sprite2D[4];
	[Export] private Button[] AxisButtons = new Button[4];
	[Export] private OptionButton AxisOptions = new OptionButton();

	[ExportGroup("Knobs")]
	[Export] public Knob OffsetKnob = new Knob();
	[Export] public Knob VelocityKnob = new Knob();

	[ExportGroup("Actions Button")]
	[Export] private Button OffsetButton = new Button();
	[Export] private Button VelocityButton = new Button();
	[Export] private Button ConfirmButton = new Button();
	[Export] private Button CancelButton = new Button();
	[Export] private Button StopButton = new Button();
	[Export] private Button ReturnToOriginButton = new Button();

	[ExportGroup("Cover")]
	[Export] private Panel PanelCover = new Panel();

	private Action?[] _AxisBtnHandlers = Array.Empty<Action?>();

	private float OffsetValue {
		get => CalibrateController.Singleton.CalibrateAxisValues.OffsetValue;
		set => CalibrateController.SetOffsetValue(value);
	}

	private float VelocityValue {
		get => CalibrateController.Singleton.CalibrateAxisValues.VelocityValue;
		set => CalibrateController.SetVelocityValue(value);
	}

	private bool CalibrateEnabled
	{
		get => CalibrateController.Singleton.CalibrateAxisValues.CalibrateEnabled;
		set => CalibrateController.SetCalibrateEnabled(value);
	}

	private MqttClasses.CalibrateAxisWheel ChoosenWheel
	{
		get => CalibrateController.Singleton.CalibrateAxisValues.ChoosenWheel;
		set => CalibrateController.SetChoosenWheel(value);
	}

	private byte ChoosenAxis
	{
		get => CalibrateController.Singleton.CalibrateAxisValues.ChoosenAxis;
		set => CalibrateController.SetChoosenAxis(value);
	}


	#region Godot.Override

	public override void _EnterTree()
	{
		// Handlers for Wheel Axis changes
		AxisOptions.ItemSelected += index => ChooseAxis(index);
		HookButtons(AxisButtons, ref _AxisBtnHandlers, HookAction.Enter, ChooseAxis);

		// Handlers for LineEdit and Sliders
		OffsetKnob.ValueChanged += newValue => OffsetValue = newValue;
		VelocityKnob.ValueChanged += newValue => VelocityValue = newValue;

		// Handlers for functional buttons
		OffsetButton.Pressed += OffsetClicked;
		VelocityButton.ButtonDown += VelocityDown;
		VelocityButton.ButtonUp += VelocityUp;
		ConfirmButton.Pressed += ConfirmClicked;
		CancelButton.Pressed += CancelClicked;
		StopButton.Pressed += StopClicked;
		ReturnToOriginButton.Pressed += ReturnToOriginClicked;

		CalibrateController.VelocityChanged += ManageVelocityChange;
		CalibrateController.OffsetSend += ManageOffsetSend;
		CalibrateController.CalibrateAxisValuesUpdated += CalibrateAxisValuesChanged;

		Connect("visibility_changed", new Callable(this, nameof(OnVisibilityChanged)));
	}

	public override void _Ready()
	{
		for (int i = 0; i < AxisModels.Length; i++)
		{
			AxisModels[i].Modulate = Color.FromHtml("#505050");
		}
		OffsetValue = Convert.ToSingle(OffsetKnob.Value);
		VelocityValue = Convert.ToSingle(VelocityKnob.Value);
		VelocityKnob.UpdateRange(
				LocalSettings.Singleton.Calibration.CalibrationMotor.MinSpeed,
				LocalSettings.Singleton.Calibration.CalibrationMotor.MaxSpeed
		);
		CalibrateEnabled = PanelCover.Visible;

		LocalSettings.Singleton.Connect(LocalSettings.SignalName.PropagatedSubcategoryChanged,
			Callable.From<StringName, StringName, Variant, Variant>(OnSettingsPropertyChanged));
	}

	public override void _ExitTree()
	{
		// Handlers for Wheel Axis changes
		AxisOptions.ItemSelected -= index => ChooseAxis(index);
		HookButtons(AxisButtons, ref _AxisBtnHandlers, HookAction.Exit, ChooseAxis);

		// Handlers for LineEdit and Sliders
		OffsetKnob.ValueChanged -= newValue => OffsetValue = newValue;
		VelocityKnob.ValueChanged -= newValue => VelocityValue = newValue;

		// Handlers for functional buttons
		OffsetButton.Pressed -= OffsetClicked;
		VelocityButton.ButtonDown -= VelocityDown;
		VelocityButton.ButtonUp -= VelocityUp;
		ConfirmButton.Pressed -= ConfirmClicked;
		CancelButton.Pressed -= CancelClicked;
		StopButton.Pressed -= StopClicked;
		ReturnToOriginButton.Pressed -= ReturnToOriginClicked;

		CalibrateController.VelocityChanged -= ManageVelocityChange;
		CalibrateController.OffsetSend -= ManageOffsetSend;
		CalibrateController.CalibrateAxisValuesUpdated -= CalibrateAxisValuesChanged;

		Disconnect("visibility_changed", new Callable(this, nameof(OnVisibilityChanged)));
		
		LocalSettings.Singleton.Disconnect(LocalSettings.SignalName.PropagatedSubcategoryChanged,
			Callable.From<StringName, StringName, Variant, Variant>(OnSettingsPropertyChanged));
	}

	#endregion Godot.Override


	#region Methods.On

	void CalibrateAxisValuesChanged() {
		UpdateChooseAxis();
	}

	void OnSettingsPropertyChanged(StringName category, StringName name, Variant oldValue, Variant newValue)
	{
		if (category != nameof(LocalSettings.Singleton.Calibration)) return;

		if (name == nameof(LocalSettings.Singleton.Calibration.CalibrationMotor)) {
			VelocityKnob.UpdateRange(
				LocalSettings.Singleton.Calibration.CalibrationMotor.MinSpeed,
				LocalSettings.Singleton.Calibration.CalibrationMotor.MaxSpeed
			);
		}
	}

	void OnVisibilityChanged() {
		CalibrateEnabled = Visible;
		if (!CalibrateEnabled)
			CancelClicked();
	}

	public Task ControlModeChangedControl(MqttClasses.ControlMode newMode)
	{
		CalibrateEnabled = (newMode == MqttClasses.ControlMode.EStop ? true : false);
		PanelCover.Visible = CalibrateEnabled ? false : true;

		if (newMode != MqttClasses.ControlMode.EStop) 	{
			CalibrateController.StopVelocitySafe();
			CancelClicked();
		}

		return Task.CompletedTask;
	}

	#endregion Methods.On


	#region Wheel.Choosing

	void ChooseAxis(long index)
	{
		if (
			CalibrateController.LastAction != CalibrateController.LastActions.Action &&
			CalibrateController.LastAction != CalibrateController.LastActions.None
		)
		{
			CalibrateController.StopVelocitySafe();

			if (CalibrateController.TryGetSelectedVescId(out var vescId))
				CalibrateController.SendCancelAsync(vescId);
		}
		ChoosenWheel = (MqttClasses.CalibrateAxisWheel)index;
		UpdateChooseAxis();
	}

	void UpdateChooseAxis()
	{
		for (int i = 0; i < AxisModels.Length; i++) {
			if (i == (int)ChoosenWheel)
			{
				AxisOptions.Select(i);
				AxisModels[i].Modulate = Color.FromHtml("#00ff00");
			}
			else
			{
				AxisModels[i].Modulate = Color.FromHtml("#505050");
			}
		}
	}

	#endregion Wheel.Choosing


	#region Knob.Changes

	void ManageVelocityChange(float newValue) {
		if(!VelocityKnob.IsSimRunning) {
			VelocityKnob.StartSim(newValue);
		}
		else {
			if(newValue != 0f) {
				VelocityKnob.UpdateSim(newValue);
			} else {
				VelocityKnob.StopSim();
			}
		}
	}

	void ManageOffsetSend(float newValue) {
		OffsetKnob.StartSimSingle(newValue);
	}

	#endregion Knob.Changes


	#region Buttons.Actions

	void VelocityDown()
	{
		if (!CalibrateController.TryGetSelectedVescId(out var vescId)) return;
		CalibrateController.StartVelocity(vescId, VelocityValue);
	}

	void VelocityUp() {
		CalibrateController.StopVelocitySafe();
	}


	void OffsetClicked()
	{
		if (!CalibrateController.TryGetSelectedVescId(out var vescId)) return;
		CalibrateController.SendOffsetAsync(vescId, OffsetValue);
	}

	void ConfirmClicked()
	{
		if (!CalibrateController.TryGetSelectedVescId(out var vescId)) return;
		CalibrateController.SendConfirmAsync(vescId);
	}

	void CancelClicked()
	{
		if (!CalibrateController.TryGetSelectedVescId(out var vescId)) return;
		CalibrateController.SendCancelAsync(vescId);
	}

	void StopClicked()
	{
		if (!CalibrateController.TryGetSelectedVescId(out var vescId)) return;
		CalibrateController.SendStopAsync(vescId);
	}

	void ReturnToOriginClicked()
	{
		if (!CalibrateController.TryGetSelectedVescId(out var vescId)) return;
		CalibrateController.SendReturnToOriginAsync(vescId);
	}

	#endregion Buttons.Actions


	void HookButtons(Button[] buttons, ref Action?[] actions, HookAction actionType, Action<long> callback)
	{

		if (buttons is null || callback is null)
			return;

		if (actions.Length != buttons.Length)
			actions = new Action?[buttons.Length];

		for (int i = 0; i < buttons.Length; i++)
		{
			var btn = buttons[i];
			if (btn is null)
				continue;

			if (actionType == HookAction.Enter)
			{
				long idx = i;
				Action handler = () => callback(idx);
				actions[i] = handler;
				btn.Pressed += handler;
			}
			else
			{
				var handler = actions[i];
				if (handler is null)
					continue;
				btn.Pressed -= handler;
				actions[i] = null;
			}
		}
	}

}
