var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;
var log = Environment.GetEnvironmentVariable("SNIPIT_FIXTURE_LOG")!;
File.AppendAllText(log, version.Major + Environment.NewLine);
if (version.Major == 1) await Task.Delay(5000);
