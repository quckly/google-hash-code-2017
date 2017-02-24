using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace GoogleHashCode2017Cs
{
    public class Solution
    {
        private int VideoCount, EndpointCount, RequestCount, CacheCount, CacheCapacity;
        private int[] VideoSize;
        private Endpoint[] Endpoints;
        private Request[] Requests;
        private CacheServer[] Caches;

        private HashSet<int> calculatedCaches = new HashSet<int>();


        public void Run(string fileName)
        {
            FillData(fileName);

            // 
            AddReqToEP();
            AddReqToCaches();
            EpTotalReqs();

            //
            ExecutePackRounds();

            // Output result
            OutputResult();
        }

        private void OutputResult()
        {
            var noEmptyCaches = Caches.Where(c => c.savedVideos.Count > 0).ToArray();

            Console.WriteLine(noEmptyCaches.Count());

            foreach (var cacheServer in noEmptyCaches)
            {
                var joined = string.Join(" ", cacheServer.savedVideos);
                Console.WriteLine($"{cacheServer.id} {joined}");
            }

#if DEBUG
            Console.ReadLine();
            Console.ReadKey();
            Console.ReadKey();
            Console.ReadKey();
#endif
        }

        private void ExecutePackRounds()
        {
            var sortedEndpoints = Endpoints.OrderByDescending(endpoint => endpoint.totalRequestes).ToArray();

            // Replace by another smart algorithm
            foreach (var endpoint in sortedEndpoints)
            {
                // That must be replaced
                var sortedCached = endpoint.caches.OrderBy(c => c.ping); // 1 -> 3 -> 10

                foreach (var cacheLink in sortedCached)
                {
                    if (calculatedCaches.Contains(cacheLink.link))
                    {
                        continue;
                    }

                    var cacheServer = Caches[cacheLink.link];

                    SelectVideosFor(cacheServer);

                    calculatedCaches.Add(cacheServer.id);

                    WriteLine($"Done {calculatedCaches.Count}/{CacheCount}");
                }
            }
        }

        private void SelectVideosFor(CacheServer cacheServer)
        {
            // CP-heat call
            var usedVideosInThatCache = Backpack(cacheServer.videoCosts);

            // Save global result
            cacheServer.savedVideos = usedVideosInThatCache;

            // Clear caches for video
            foreach (var usedVideo in usedVideosInThatCache)
            {
                foreach (var downFromCurrentCacheToAllHimEP in cacheServer.videoUsedBy[usedVideo])
                {
                    var endpoint = Endpoints[downFromCurrentCacheToAllHimEP.endpoint];
                    var globalPing = endpoint.pingToDC;

                    var considerReq = endpoint.video2RequestsMap[usedVideo];

                    foreach (var considerCacheToClearLink in endpoint.caches)
                    {
                        var considerCacheToClear = Caches[considerCacheToClearLink.link];
                        var cachePing = considerCacheToClearLink.ping;

                        if (considerCacheToClear.videoUsedBySet.Contains(considerReq))
                        {
                            considerCacheToClear.videoUsedBySet.Remove(considerReq);

                            considerCacheToClear.videoCosts[usedVideo] -= (globalPing - cachePing) * considerReq.count;
                        }
                    }
                }

            }
        }

        List<int> Backpack(Dictionary<int, long> videoCosts)
        {
            return HashCodeQualification.Knapsack.GetVideos(videoCosts, VideoSize, CacheCapacity);

            //return SortedFirst(videoCosts);
        }

        List<int> SortedFirst(Dictionary<int, long> videoCosts)
        {
            List<int> result = new List<int>();
            long summ = 0;

            var ccc = videoCosts.OrderByDescending(pair => pair.Value);

            foreach (var videoCostsKV in ccc)
            {
                var ps = summ + VideoSize[videoCostsKV.Key];

                if (ps > CacheCapacity)
                    continue;

                summ = ps;
                result.Add(videoCostsKV.Key);
            }

            return result;
        }

        void EpTotalReqs()
        {
            foreach (var endpoint in Endpoints)
            {
                long sum = 0;

                foreach (var request in endpoint.requests)
                {
                    sum += request.count;
                }

                endpoint.totalRequestes = sum;
            }
        }

        void AddReqToCaches()
        {
            foreach (var endpoint in Endpoints)
            {
                if (endpoint.id > 0 && endpoint.id % 100 == 0)
                    WriteLine($"Endpoint init {endpoint.id}/{EndpointCount}");

                var globalPing = endpoint.pingToDC;

                foreach (var request in endpoint.requests)
                {
                    foreach (var endpointCache in endpoint.caches)
                    {
                        var cacheServer = Caches[endpointCache.link];
                        var cachePing = endpointCache.ping;

                        cacheServer.allRequests.Add(request);

                        // Aggregate request from EP to video map
                        var calcRequestCost = (globalPing - cachePing) * request.count;

                        if (cacheServer.videoCosts.ContainsKey(request.video))
                        {
                            cacheServer.videoCosts[request.video] += calcRequestCost;
                        }
                        else
                        {
                            cacheServer.videoCosts.Add(request.video, calcRequestCost);
                        }

                        // Add index from cache to endpoints that used video
                        if (cacheServer.videoUsedBy.ContainsKey(request.video))
                        {
                            cacheServer.videoUsedBy[request.video].Add(request);
                        }
                        else
                        {
                            var temp = new List<Request> { request };
                            cacheServer.videoUsedBy.Add(request.video, temp);
                        }

                        cacheServer.videoUsedBySet.Add(request);
                    }
                }
            }
        }

        void AddReqToEP()
        {
            foreach (var request in Requests)
            {
                // Ignore videos that weight more cache capacity
                if (VideoSize[request.video] > CacheCapacity)
                {
                    continue;
                }

                var endpoint = Endpoints[request.endpoint];
                
                // Create index
                if (endpoint.video2RequestsMap.ContainsKey(request.video))
                {
                    endpoint.video2RequestsMap[request.video].count += request.count; // Agregate
                }
                else
                {
                    endpoint.video2RequestsMap.Add(request.video, request);

                    endpoint.requests.Add(request);
                }
            }
        }

        private void FillData(string fileName)
        {
            if (fileName == null)
            {
                fileName = "trending_today.in";
            }

            using (var sr = new StreamReader(fileName, Encoding.ASCII))
            {
                //
                var inputA = sr.ReadLine().Split(' ').Select(x => Int32.Parse(x)).ToArray();
                VideoCount = inputA[0];
                EndpointCount = inputA[1];
                RequestCount = inputA[2];
                CacheCount = inputA[3];
                CacheCapacity = inputA[4];

                Endpoints = new Endpoint[EndpointCount];
                Requests = new Request[RequestCount];
                Caches = new CacheServer[CacheCount];

                for (int cacheId = 0; cacheId < CacheCount; cacheId++)
                {
                    Caches[cacheId] = new CacheServer {id = cacheId};
                }

                //
                inputA = sr.ReadLine().Split(' ').Select(x => Int32.Parse(x)).ToArray();

                VideoSize = inputA;

                // Endpoints
                for (int endpointId = 0; endpointId < EndpointCount; endpointId++)
                {
                    var e = new Endpoint();

                    inputA = sr.ReadLine().Split(' ').Select(x => Int32.Parse(x)).ToArray();
                    e.pingToDC = inputA[0];
                    e.id = endpointId;
                    var linkCount = inputA[1];

                    for (int cs = 0; cs < linkCount; cs++)
                    {
                        inputA = sr.ReadLine().Split(' ').Select(x => Int32.Parse(x)).ToArray();

                        e.caches.Add(new CacheLink(inputA[0], inputA[1]));

                        // Add link to cache and vice versa
                        Caches[inputA[0]].endpoints.Add(new CacheLink(endpointId, inputA[1]));
                    }

                    //
                    Endpoints[endpointId] = e;
                }

                // Requestes
                for (int requestId = 0; requestId < RequestCount; requestId++)
                {
                    inputA = sr.ReadLine().Split(' ').Select(x => Int32.Parse(x)).ToArray();

                    //
                    var request = new Request(requestId, inputA[0], inputA[1], inputA[2]);
                    Requests[requestId] = request;
                }
            }
        }

        private void WriteLine(string s)
        {
#if DEBUG || true
            Console.WriteLine(s);
#endif
        }
    }

    public class CacheLink
    {
        public int link;
        public int ping;

        public CacheLink(int link, int ping)
        {
            this.link = link;
            this.ping = ping;
        }
    }

    public class CacheServer
    {
        public int id;
        public List<CacheLink> endpoints = new List<CacheLink>();
        public List<Request> allRequests = new List<Request>();
        public Dictionary<int, long> videoCosts = new Dictionary<int, long>();
        public Dictionary<int, List<Request>> videoUsedBy = new Dictionary<int, List<Request>>();
        public HashSet<Request> videoUsedBySet = new HashSet<Request>();

        // Result
        public List<int> savedVideos = new List<int>();
    }

    public class Endpoint
    {
        public int id;
        public int pingToDC;
        public List<CacheLink> caches = new List<CacheLink>();

        public List<Request> requests = new List<Request>();
        public Dictionary<int, Request> video2RequestsMap = new Dictionary<int, Request>();
        public long totalRequestes;
    }

    public class Request
    {
        public int id;
        public int video;
        public int endpoint;
        public int count;

        public Request(int id, int video, int endpoint, int count)
        {
            this.id = id;
            this.video = video;
            this.endpoint = endpoint;
            this.count = count;
        }
    }
}

namespace HashCodeQualification
{
    class Knapsack
    {
        public long[][] M { get; set; }
        public long[][] P { get; set; }
        public Item[] I { get; set; }
        public static long MaxValue { get; private set; }
        public long W { get; set; }

        public Knapsack(List<Item> items, int maxWeight)
        {
            I = items.ToArray();
            W = maxWeight;

            var n = I.Length;
            M = new long[n + 1][];
            P = new long[n + 1][];
            for (var i = 0; i < M.Length; i++) { M[i] = new long[W + 1]; }
            for (var i = 0; i < P.Length; i++) { P[i] = new long[W + 1]; }
        }

        public static List<int> GetVideos(Dictionary<int, long> videoCosts,
            int[] videoSize, int MaxWeight)
        {
            var items = new List<Item>();
            foreach (var pair in videoCosts)
            {
                items.Add(new Item
                {
                    Weight = videoSize[pair.Key],
                    Cost = pair.Value,
                    MyId = pair.Key
                });
            }

            Knapsack ks = new Knapsack(items, MaxWeight);
            ks.Run();
            return ks.GetItems();
        }

        public void Run()
        {
            var n = I.Length;

            for (var i = 1; i <= n; i++)
            {
                for (var j = 0; j <= W; j++)
                {
                    if (I[i - 1].Weight <= j)
                    {
                        M[i][j] = Max(M[i - 1][j], I[i - 1].Cost + M[i - 1][j - I[i - 1].Weight]);
                        if (I[i - 1].Cost + M[i - 1][j - I[i - 1].Weight] > M[i - 1][j])
                        {
                            P[i][j] = 1;
                        }
                        else
                        {
                            P[i][j] = -1;
                        }
                    }
                    else
                    {
                        P[i][j] = -1;
                        M[i][j] = M[i - 1][j];
                    }
                }
            }
            MaxValue = M[n][W];
        }

        public List<int> GetItems()
        {
            List<int> ids = new List<int>();
            var list = new List<Item>();
            list.AddRange(I);
            long w = W;
            var i = list.Count;

            long valueSum = 0;
            long weightSum = 0;
            while (i >= 0 && w >= 0)
            {
                if (P[i][w] == 1)
                {
                    valueSum += list[i - 1].Cost;
                    weightSum += list[i - 1].Weight;
                    ids.Add(list[i - 1].MyId);


                    w -= list[i - 1].Weight;
                }

                i--;
            }

            return ids;
        }


        static long Max(long a, long b)
        {
            return a > b ? a : b;
        }
    }


    class Item
    {
        private static int _counter;
        public int Id { get; private set; }
        public long Cost { get; set; } // value (запросы)
        public long Weight { get; set; } // weight (мегабайты)
        public int MyId { get; set; }

        public Item()
        {
            Id = ++_counter;
        }

        public override string ToString()
        {
            return string.Format("Id: {0}  v: {1}  w: {2}",
                                 Id, Cost, Weight);
        }
    }
}
