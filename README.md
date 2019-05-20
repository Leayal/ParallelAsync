# ParallelAsync
.NET System.Threading.Tasks.Parallel but with async.

## Example
```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;

namespace ParallelAsync_Test
{
    class Program
    {
        // C# 7.3 feature???? (Async Main)
        public static async Task Main(string[] args)
        {
            // Prepare
            string formatString = "http://example.com/text_file_{0}.txt";
            Uri[] uris = new Uri[50];
            for (int i = 0; i < uris.Length; i++)
            {
                uris[i] = new Uri(string.Format(formatString, i));
            }

            HttpClient httpClient = new HttpClient();
            ConcurrentDictionary<Uri, string> result = new ConcurrentDictionary<Uri, string>();

            // Allow Ctrl+C to cancel the async parallel download
            CancellationTokenSource cancelSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                cancelSource.Cancel();
            };

            // Download 4 files in parallel until all 50 files are downloaded or until user request cancellation.
            var loopResult = await ParallelAsync.ForEach(uris, new ParallelOptions() { MaxDegreeOfParallelism = 4, CancellationToken = cancelSource.Token }, async (uri) =>
            {
                result.TryAdd(uri, await httpClient.GetStringAsync(uri));
            });
            
            // Print the downloaded URLs and their text data.
            foreach (var pair in result)
            {
                Console.WriteLine("==================================");
                Console.WriteLine("URL: " + pair.Key.OriginalString);
                Console.WriteLine("Data: " + pair.Value);
                Console.WriteLine("==================================");
            }

            if (!loopResult.IsCompleted)
            {
                Console.WriteLine();
                Console.WriteLine("Loop ended prematurely because user has requested cancellation. Completed {0} loops.", loopResult.LowestBreakIteration.Value);
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to close.");
            Console.ReadKey();
        }
    }
}
```