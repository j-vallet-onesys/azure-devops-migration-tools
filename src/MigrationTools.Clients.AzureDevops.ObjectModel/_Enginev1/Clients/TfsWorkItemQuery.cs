﻿using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using MigrationTools.DataContracts;
using Serilog;

namespace MigrationTools._EngineV1.Clients
{
    public class TfsWorkItemQuery : WorkItemQueryBase
    {
        public TfsWorkItemQuery(IServiceProvider services, ITelemetryLogger telemetry) : base(services, telemetry)
        {
        }

        public override List<WorkItemData> GetWorkItems()
        {
            Log.Debug("WorkItemQuery: ===========GetWorkItems=============");
            var wiClient = (TfsWorkItemMigrationClient)MigrationClient.WorkItems;
            Telemetry.TrackEvent("WorkItemQuery.Execute", Parameters, null);
            Log.Debug("WorkItemQuery: TeamProjectCollection: {QueryTarget}", wiClient.Store.TeamProjectCollection.Uri.ToString());
            Log.Debug("WorkItemQuery: Query: {QueryText}", Query);
            Log.Debug("WorkItemQuery: Paramiters: {@QueryParams}", Parameters);
            foreach (var item in Parameters)
            {
                Log.Debug("WorkItemQuery: {0}: {1}", item.Key, item.Value);
            }
            return GetWorkItemsFromQuery(wiClient).ToWorkItemDataList();
        }

        public override List<MigrationTools.DataContracts.WorkItemData> GetWorkItems2()
        {
            throw new NotImplementedException();
        }

        private IList<WorkItem> GetWorkItemsFromQuery(TfsWorkItemMigrationClient wiClient)
        {
            var startTime = DateTime.UtcNow;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            List<WorkItem> results = new List<WorkItem>();
            WorkItemCollection workItemCollection;
            try
            {
                // Bidouille pour changement de nom de projet
                string newQuery = wiClient.Project.Name == "P2"
                    ? Query
                    : Query.Replace("'P2\\", $"'{wiClient.Project.Name}\\");
                workItemCollection = wiClient.Store.Query(newQuery);
                foreach (WorkItem item in workItemCollection)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(item.Title)) // Force to read WI
                            results.Add(item);
                    }
                    catch (DeniedOrNotExistException ex)
                    {
                        Log.Error(ex, "Deleted Item detected.");
                    }
                }
                timer.Stop();
                Telemetry.TrackDependency(new DependencyTelemetry("TfsObjectModel", MigrationClient.Config.AsTeamProjectConfig().Collection.ToString(), "GetWorkItemsFromQuery", null, startTime, timer.Elapsed, "200", true));
            }
            catch (Exception ex)
            {
                timer.Stop();
                Telemetry.TrackDependency(new DependencyTelemetry("TfsObjectModel", MigrationClient.Config.AsTeamProjectConfig().Collection.ToString(), "GetWorkItemsFromQuery", null, startTime, timer.Elapsed, "500", false));
                Telemetry.TrackException(ex,
                       new Dictionary<string, string> {
                            { "CollectionUrl", wiClient.Store.TeamProjectCollection.Uri.ToString() }
                       },
                       new Dictionary<string, double> {
                            { "QueryTime",timer.ElapsedMilliseconds }
                       });
                Log.Error(ex, " Error running query");
                throw;
            }
            return results;
        }
    }
}