﻿using Aether.Devices.Displays.Themes;
using Aether.Devices.Sensors;
using Aether.Devices.Sensors.Metadata;
using Aether.Devices.Simulated;
using Aether.Reactive;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using UnitsNet;

var listSensorCommand = new Command("list", "Lists available sensors")
{
    Handler = CommandHandler.Create(() =>
    {
        foreach (SensorInfo sensorInfo in SensorInfo.Sensors)
        {
            string type = sensorInfo switch
            {
                I2cSensorInfo i2c => $"i2c(0x{i2c.DefaultAddress:X2})",
                _ => throw new Exception($"Unknown {nameof(SensorInfo)} subclass.")
            };

            Console.WriteLine($"{type}{(sensorInfo.CanSimulateSensor ? " / simulatable" : "              ")} - {sensorInfo.Name} - {string.Join(", ", sensorInfo.Measures)}");
        }
    })
};

var testi2cSensorCommand = new Command("i2c", "Tests an I2C sensor")
{
    new Argument<string>("name", "The name of the sensor to test."),
    new Argument<uint>("bus", "The I2C bus to use."),
    new Argument<uint>("address", "The I2C address to use.")
};

testi2cSensorCommand.Handler = CommandHandler.Create((string name, uint bus, uint address) => RunAndPrintSensorAsync(() =>
{
    SensorInfo? sensorInfo = SensorInfo.Sensors.FirstOrDefault(x => x is I2cSensorInfo && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));

    return sensorInfo switch
    {
        I2cSensorInfo i2c => i2c.OpenDevice((int)bus, (int)address, Observable.Empty<Measurement>()),
        _ => throw new Exception("An I2C sensor by that name was not found.")
    };
}));

var simulateSensorCommand = new Command("simulate", "Simulates a sensor")
{
    new Argument<string>("name", "The name of the sensor to test.")
};

simulateSensorCommand.Handler = CommandHandler.Create((string name) => RunAndPrintSensorAsync(() =>
{
    SensorInfo sensorInfo = SensorInfo.Sensors.FirstOrDefault(x => x is I2cSensorInfo { CanSimulateSensor: true } && string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new Exception("A simulatable sensor by that name was not found.");

    return sensorInfo.CreateSimulatedSensor(Observable.Empty<Measurement>());
}));

// Temporary command to test the theme.
// TODO: Make this more like a list/test format similar to sensor.
var themeTestCommand = new Command("theme-test", "Tests a theme.");
themeTestCommand.Handler = CommandHandler.Create(() =>
{
    var lines = new[] { Measure.CO2, Measure.Humidity, Measure.BarometricPressure, Measure.Temperature };

    using var driver = new SimulatedDisplayDriver("out", 296, 128, 112.399461802960f, 111.917383820998f);
    using var sub = new Subject<Measurement>();
    using IDisposable theme = MultiLineTheme.CreateTheme(driver, lines, sub);

    sub.OnNext(Measurement.FromCo2(VolumeConcentration.FromPartsPerMillion(4312.25)));
    sub.OnNext(Measurement.FromRelativeHumidity(RelativeHumidity.FromPercent(59.1)));
    sub.OnNext(Measurement.FromPressure(Pressure.FromAtmospheres(1.04)));
    sub.OnNext(Measurement.FromTemperature(Temperature.FromDegreesFahrenheit(65.2)));
    sub.OnCompleted();
});

var rootCommand = new RootCommand()
{
    new Command("sensor", "Operates on sensors")
    {
        listSensorCommand,
        new Command("test", "Tests a sensor")
        {
            testi2cSensorCommand,
        },
        simulateSensorCommand
    },
    themeTestCommand
};

await rootCommand.InvokeAsync(Environment.CommandLine);

static Task RunAndPrintSensorAsync(Func<ObservableSensor> sensorFunc) =>
    AetherObservable.AsyncUsing(sensorFunc, sensor => sensor)
    .TakeUntil(AetherObservable.ConsoleCancelKeyPress)
    .ForEachAsync(measurement =>
    {
        Console.WriteLine($"[{DateTime.Now:t}] {measurement}");
    });
