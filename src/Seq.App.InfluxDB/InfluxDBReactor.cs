using System;
using System.Collections.Generic;
using System.Linq;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Models;
using Seq.Apps;
using Seq.Apps.LogEvents;

// ReSharper disable InconsistentNaming
namespace Seq.App.InfluxDB
{
    public class InfluxDBReactor : Reactor, ISubscribeTo<LogEventData>
    {
        private readonly InfluxDbClient _influxManager;

        private readonly Dictionary<string, object> _tags; 

        public InfluxDBReactor()
        {
            _influxManager = new InfluxDbClient(InfluxDBEndpoint, Username, Password, InfluxDbVersion.Latest);


            _tags = Tags.Split(',')
                .Select(t => t.Split('|'))
                .Where(t => t.Length > 1)
                .ToDictionary(k => k[0], v => (object)v[1]);
        }

        [SeqAppSetting(
            DisplayName = "Database",
            HelpText = "InfluxDB database you want to log to.")]
        public string Database { get; set; }

        [SeqAppSetting(
            DisplayName = "Password",
            HelpText = "Password of the account you want to log as.", 
            InputType = SettingInputType.Password)]
        public string Password { get; set; }

        [SeqAppSetting(
            DisplayName = "Username",
            HelpText = "Username of the account you want to log as.")]
        public string Username { get; set; }

        [SeqAppSetting(
            DisplayName = "InfluxDB Url",
            HelpText = "This is the Url including the port that you want to send the Measurements to.")]
        public string InfluxDBEndpoint { get; set; }

        [SeqAppSetting(
            DisplayName = "Measurement Name",
            HelpText = "Measurement name you want to log to InfluxDB")]
        public string MeasurementName { get; set; }

        [SeqAppSetting(
            DisplayName = "Field Name",
            HelpText = "Field name you want to log to InfluxDB")]
        public string FieldName { get; set; }

        [SeqAppSetting(
            DisplayName = "Tags",
            HelpText = "Pipe to separate tag key and tag value and a comma to separate multiple tag pairs. Eg. host|server1,az|syd1")]
        public string Tags { get; set; }

        public void On(Event<LogEventData> evt)
        {
            try
            {

                var pointToWrite = new Point()
                {
                    Name = MeasurementName, // serie/measurement/table to write into
                    Tags = _tags,
                    Fields = new Dictionary<string, object>()
                    {
                        {FieldName, 1}
                    },
                    Timestamp = DateTime.UtcNow // optional (can be set to any DateTime moment)
                };

                var response = _influxManager.Client.WriteAsync(Database, pointToWrite);
                response.Wait();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error when sending measurement to InfluxDB");
            }
        }
    }
}