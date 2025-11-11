using Godot;
using MQTTnet;
using RoverControlApp.Core;
using RoverControlApp.Core.Settings;
using RoverControlApp.MVVM.Model;
using System;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static RoverControlApp.Core.MqttClasses;

public partial class CustomScene : Panel {

	private string topic = LocalSettings.Singleton.Mqtt.TopicZedImuData;

	private TopicData TextData = null!;

	[Export]
	private Label RotateText = null!;

	class TopicData	{
		public double RotSpeed { get; set; } = 4;
		public string Text { get; set; } = "Rotate";
	}

	public override void _EnterTree() {
		MqttNode.Singleton.MessageReceivedAsync += TopicDataInfoChanged;
	}

	public override void _ExitTree() {
		MqttNode.Singleton.MessageReceivedAsync -= TopicDataInfoChanged;
	}

	public override void _Ready() {
		TextData = new TopicData();
	}

	// Testowane na  https://testclient-cloud.mqtt.cool/
	// broker.hivemq.com, 1883

	// RappTORS/TopicZedImuData
	// {"RotSpeed":2.23,"Text":"Jakistamtekst"}

	public override void _Process(double delta) {
		RotateText.Rotation += (float)delta * (float)TextData.RotSpeed;
		RotateText.Text = TextData.Text;
	}

	public async Task TopicDataInfoChanged(string subTopic, MqttApplicationMessage? msg) {

		if (string.IsNullOrEmpty(topic) || subTopic != topic)	{
			return;
		}
		
		if (msg is null || msg.PayloadSegment.Count == 0) {
			EventLogger.LogMessage("CustomScene", EventLogger.LogLevel.Error, "Empty payload");
			return;
		}

		TextData = JsonSerializer.Deserialize<TopicData>(msg.ConvertPayloadToString())!;

		return;
	}


	public override void _Input(InputEvent @event) {

		if (@event is InputEventKey inputEventKey) {

			if (inputEventKey.KeyLabel.ToString() == "R") {
				RotateText.AddThemeColorOverride("font_color", new Color(255, 0, 0));
			}

			if (inputEventKey.KeyLabel.ToString() == "G") {
				RotateText.AddThemeColorOverride("font_color", new Color(0, 255, 0));
			}

			if (inputEventKey.KeyLabel.ToString() == "B")	{
				RotateText.AddThemeColorOverride("font_color", new Color(0, 0, 255));
			}

		}

	}

}
