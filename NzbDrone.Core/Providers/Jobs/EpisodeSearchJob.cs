﻿using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Model;
using NzbDrone.Core.Model.Notification;
using NzbDrone.Core.Providers.Indexer;
using NzbDrone.Core.Repository;

namespace NzbDrone.Core.Providers.Jobs
{
    public class EpisodeSearchJob : IJob
    {
        private readonly InventoryProvider _inventoryProvider;
        private readonly DownloadProvider _downloadProvider;
        private readonly IndexerProvider _indexerProvider;
        private readonly EpisodeProvider _episodeProvider;


        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public EpisodeSearchJob(InventoryProvider inventoryProvider, DownloadProvider downloadProvider, IndexerProvider indexerProvider, EpisodeProvider episodeProvider)
        {
            _inventoryProvider = inventoryProvider;
            _downloadProvider = downloadProvider;
            _indexerProvider = indexerProvider;
            _episodeProvider = episodeProvider;
        }

        public string Name
        {
            get { return "Episode Search"; }
        }

        public int DefaultInterval
        {
            get { return 0; }
        }

        public void Start(ProgressNotification notification, int targetId)
        {
            var reports = new List<EpisodeParseResult>();
            var episode = _episodeProvider.GetEpisode(targetId);
            var indexers = _indexerProvider.GetEnabledIndexers();

            foreach (var indexer in indexers)
            {
                try
                {
                    notification.CurrentMessage = String.Format("Searching for {0} in {1}", episode, indexer.Name);
                    reports.AddRange(indexer.FetchRss());
                }
                catch (Exception e)
                {
                    Logger.ErrorException("An error has occurred while fetching items from " + indexer.Name, e);
                }
            }

            Logger.Debug("Finished searching all indexers. Total {0}", reports.Count);
            notification.CurrentMessage = "Processing search results";

            ProcessResults(notification, episode, reports);
        }

        public void ProcessResults(ProgressNotification notification, Episode episode, IEnumerable<EpisodeParseResult> reports)
        {
            foreach (var episodeParseResult in reports.OrderByDescending(c => c.Quality).ThenByDescending(c => c.Proper))
            {
                try
                {
                    if (_inventoryProvider.IsNeeded(episodeParseResult))
                    {
                        _downloadProvider.DownloadReport(episodeParseResult);
                        notification.CurrentMessage = String.Format("{0} {1} Added to download queue", episode, episodeParseResult.Quality);
                        return;
                    }
                }
                catch (Exception e)
                {
                    Logger.ErrorException("An error has occurred while processing parse result items from " + episodeParseResult, e);
                }
            }
            
            Logger.Warn("Unable to find {0} in any of indexers.", episode);
        }
    }
}