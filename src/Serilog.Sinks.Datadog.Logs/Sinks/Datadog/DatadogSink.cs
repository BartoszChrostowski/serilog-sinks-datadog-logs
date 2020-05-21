﻿// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0.
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2019 Datadog, Inc.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog.Events;
using Serilog.Formatting.Json;
using Serilog.Sinks.PeriodicBatching;
using Newtonsoft.Json;

namespace Serilog.Sinks.Datadog.Logs
{
    public class DatadogSink : PeriodicBatchingSink
    {
        private readonly IDatadogClient _client;

        /// <summary>
        /// Shared JSON formatter.
        /// </summary>
        private static readonly JsonFormatter formatter = new JsonFormatter(renderMessage: true);

        /// <summary>
        /// The time to wait before emitting a new event batch.
        /// </summary>
        private static readonly TimeSpan DefaultBatchPeriod = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Settings to drop null values.
        /// </summary>
        private static readonly JsonSerializerSettings settings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        /// <summary>
        /// The maximum number of events to emit in a single batch.
        /// </summary>
        private const int DefaultBatchSizeLimit = 50;

        /// <summary>
        /// Maximum number of events to hold in the sink's internal queue, or <c>null</c>
        /// for an unbounded queue. The default is <c>10000</c>.
        /// </summary>
        private const int DefaultQueueLimit = 10000;

        public DatadogSink(string apiKey, string source, string service, string host, string[] tags, DatadogConfiguration config, int? batchSizeLimit = null, TimeSpan? batchPeriod = null)
            : base(batchSizeLimit ?? DefaultBatchSizeLimit, batchPeriod ?? DefaultBatchPeriod)
        {
            if (config.UseTCP)
            {
                _client = new DatadogTcpClient(config, new LogFormatter(source, service, host, tags), apiKey);
            }
            else
            {
                _client = new DatadogHttpClient(config, new LogFormatter(source, service, host, tags), apiKey);
            }
        }

        public DatadogSink(string apiKey, string source, string service, string host, string[] tags, DatadogConfiguration config, int? batchSizeLimit = null, TimeSpan? batchPeriod = null, int? queueLimit = null)
            : base(batchSizeLimit ?? DefaultBatchSizeLimit, batchPeriod ?? DefaultBatchPeriod, queueLimit ?? DefaultQueueLimit)
        {
            if (config.UseTCP)
            {
                _client = new DatadogTcpClient(config, new LogFormatter(source, service, host, tags), apiKey);
            }
            else
            {
                _client = new DatadogHttpClient(config, new LogFormatter(source, service, host, tags), apiKey);
            }
        }

        public static DatadogSink Create(string apiKey, string source, string service, string host, string[] tags, DatadogConfiguration config, int? batchSizeLimit = null, TimeSpan? batchPeriod = null, int? queueLimit = null)
        {
            if (queueLimit.HasValue)
                return new DatadogSink(apiKey, source, service, host, tags, config, batchSizeLimit, batchPeriod, queueLimit);

            return new DatadogSink(apiKey, source, service, host, tags, config, batchSizeLimit, batchPeriod);
        }

        /// <summary>
        /// Emit a batch of log events to Datadog logs-backend.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        protected override async Task EmitBatchAsync(IEnumerable<LogEvent> events)
        {
            if (!events.Any())
            {
                return;
            }

            var tasks = _client.WriteAsync(events);
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Free resources held by the sink.
        /// </summary>
        /// <param name="disposing">If true, called because the object is being disposed; if false,
        /// the object is being disposed from the finalizer.</param>
        protected override void Dispose(bool disposing)
        {
            _client.Close();
            base.Dispose(disposing);
        }
    }
}
