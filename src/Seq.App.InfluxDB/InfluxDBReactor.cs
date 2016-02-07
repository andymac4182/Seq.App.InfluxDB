using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Models;
using Seq.Apps;
using Seq.Apps.LogEvents;

// ReSharper disable InconsistentNaming
namespace Seq.App.InfluxDB
{
    [SeqApp("InfluxDB Graphing",
        Description = "Reports 1 value to InfluxDB for each item logged.")]
    public class InfluxDBReactor : Reactor, ISubscribeTo<LogEventData>
    {
        private InfluxDbClient _influxManager;

        private BlockingCollection<Point> _pointsCollection { get; set; }

        private Dictionary<string, object> _tags = new Dictionary<string, object>();

        private List<string> _propertiesToIncludeAsTags = new List<string>(); 

        private List<string> _propertiesToIncludeAsFields = new List<string>();

        public InfluxDBReactor()
        {
            _pointsCollection = new BlockingCollection<Point>();
        }

        protected override void OnAttached()
        {
            try
            {
                _influxManager = new InfluxDbClient(InfluxDBEndpoint, Username, Password, InfluxDbVersion.Latest);

                if (!string.IsNullOrWhiteSpace(Tags))
                {
                    _tags = Tags.Split(',')
                        .Select(t => t.Split('|'))
                        .Where(t => t.Length > 1)
                        .ToDictionary(k => k[0], v => (object) v[1]);
                }

                if (!string.IsNullOrWhiteSpace(FieldValue))
                {
                    _propertiesToIncludeAsFields =
                        FieldValue.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                if (!string.IsNullOrWhiteSpace(PropertiesAsTags))
                {
                    _propertiesToIncludeAsTags =
                        PropertiesAsTags.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                RegisterReadingsQueueHandler();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error starting Seq.Apps.InfluxDB");
                throw;
            }
            
        }

        [SeqAppSetting(
            DisplayName = "InfluxDB Url",
            HelpText = "This is the Url including the port that you want to send the Measurements to.")]
        public string InfluxDBEndpoint { get; set; }

        [SeqAppSetting(
            DisplayName = "Database",
            HelpText = "InfluxDB database you want to log to.")]
        public string Database { get; set; }

        [SeqAppSetting(
            DisplayName = "Username",
            HelpText = "Username of the account you want to log as.")]
        public string Username { get; set; }

        [SeqAppSetting(
            DisplayName = "Password",
            HelpText = "Password of the account you want to log as.", 
            InputType = SettingInputType.Password)]
        public string Password { get; set; }

        [SeqAppSetting(
            DisplayName = "Measurement Name",
            HelpText = "Measurement name you want to log to InfluxDB")]
        public string MeasurementName { get; set; }

        [SeqAppSetting(
            DisplayName = "Field Name",
            HelpText = "Field name you want to log to InfluxDB if no properties are specified.")]
        public string FieldName { get; set; }

        [SeqAppSetting(
            DisplayName = "Field Value",
            HelpText = "Property to load field value from. Comma separated list of properties to send as fields. If no property matches then will use 1 as the value and the name from Field Name.", 
            IsOptional = true)]
        public string FieldValue { get; set; }

        [SeqAppSetting(
            DisplayName = "Tags",
            HelpText = "Pipe to separate tag key and tag value and a comma to separate multiple tag pairs. Eg. host|server1,az|syd1",
            IsOptional = true)]
        public string Tags { get; set; }

        [SeqAppSetting(
            DisplayName = "Properties to include as Tags",
            HelpText = "Comma separated list of properties to include as tags if they are logged.", 
            IsOptional = true)]
        public string PropertiesAsTags { get; set; }

        public void On(Event<LogEventData> evt)
        {
            try
            {
                var currentTags = new Dictionary<string, object>(_tags);

                var currentFields = new Dictionary<string, object>()
                {
                    {FieldName, 1}
                };
                
                foreach (var property in evt.Data.Properties.Where(p => _propertiesToIncludeAsTags.Contains(p.Key)))
                {
                    currentTags.Add(property.Key, property.Value);
                }

                var fieldsToInclude = evt.Data.Properties.Where(p => _propertiesToIncludeAsFields.Contains(p.Key)).ToList();

                if (fieldsToInclude.Any())
                {
                    currentFields = fieldsToInclude.ToDictionary(p => p.Key, p => p.Value);
                }

                var pointToWrite = new Point()
                { 
                    Name = MeasurementName, 
                    Tags = currentTags,
                    Fields = currentFields,
                    Timestamp = evt.TimestampUtc
                };

                _pointsCollection.Add(pointToWrite);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error when sending measurement to InfluxDB");
            }
        }

        private async Task RegisterReadingsQueueHandler()
        {
            while (true)
            {
                await Task.Delay(1000);
                HandleReadingsQueue();
            }
        }

        private async Task HandleReadingsQueue()
        {
            var readingsCount = _pointsCollection.Count;
            IList<Point> readings = new List<Point>();

            for (var i = 0; i < readingsCount; i++)
            {
                Point reading;
                var dequeueSuccess = _pointsCollection.TryTake(out reading);

                if (dequeueSuccess)
                {
                    readings.Add(reading);
                }
                else
                {
                    throw new Exception("Could not dequeue the collection");
                }
            }

            if (readings.Count > 0)
            {
                await _influxManager.Client.WriteAsync(Database, readings);
            }
        }
    }
}
