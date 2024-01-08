// using System.Diagnostics;
// using FabulousScheduler.Cron.Interfaces;
// using FabulousScheduler.Queue;
// using FabulousScheduler.Queue.Interfaces;
// using FabulousScheduler.Queue.Queues;
//
// namespace Job.Core.Tests.Queue;
//
// public class JobManagerTests
// {
// 	[Fact]
// 	public async void TestExecutingJob()
// 	{
// 		const int oneTimeJobMs = 25;
// 		var conf = new Config(1, TimeSpan.FromSeconds(0.2));
// 		var queue = new MemoryQueue();
//
// 		var manager = new TestQueueReAddFailJobManager(queue, conf, 5);
// 		manager.Start();
// 		var job = new QJob_Fail("690", TimeSpan.FromMilliseconds(oneTimeJobMs));
// 		queue.Enqueue(job);
//
// 		var sw = Stopwatch.StartNew();
// 		await manager.FinishAllAndStopAsync();
// 		sw.Stop();
//
// 		double errorCalc = 15;
// 		
// 		Assert.InRange(sw.Elapsed.TotalMilliseconds, oneTimeJobMs-errorCalc, oneTimeJobMs+errorCalc);
// 	}
// 	
// 	[Fact]
// 	public async void TestParallelExecutingJobs_5k()
// 	{
// 		int countJobs = 5000, oneTimeJobMs = 25, parallelJobs = 50;
// 		var conf = new Config(parallelJobs, TimeSpan.FromSeconds(0.2));
// 		var queue = new MemoryQueue();
//
// 		var manager = new TestQueueReAddFailJobManager(queue, conf, 1);
// 		manager.Start();
// 		for (int i = 1; i <= countJobs; i++)
// 		{
// 			var job = new QJob_Random(i.ToString(), TimeSpan.FromMilliseconds(oneTimeJobMs));
// 			queue.Enqueue(job);
// 		}
// 		
// 		var sw = Stopwatch.StartNew();
// 		await manager.FinishAllAndStopAsync();
// 		sw.Stop();
// 		
// 		double shouldWorkSecond = (countJobs / (countJobs >= parallelJobs ? parallelJobs : 1)) * (oneTimeJobMs / 1000.0 /*in sec*/);
// 		double errorCalc = 1;
// 		
// 		Assert.InRange(sw.Elapsed.TotalSeconds, shouldWorkSecond-errorCalc, shouldWorkSecond+errorCalc);
// 		Assert.Equal(0, queue.Count);
// 	}
// 	
// 	[Fact]
// 	public async void TestParallelExecutingJobs_50k()
// 	{
// 		int countJobs = 50000, oneTimeJobMs = 5, parallelJobs = 50;
// 		var conf = new Config(parallelJobs, TimeSpan.FromSeconds(0.2));
// 		var queue = new MemoryQueue();
//
// 		var manager = new TestQueueReAddFailJobManager(queue, conf, 1);
// 		manager.Start();
// 		for (int i = 1; i <= countJobs; i++)
// 		{
// 			var job = new QJob_Random(i.ToString(), TimeSpan.FromMilliseconds(oneTimeJobMs));
// 			queue.Enqueue(job);
// 		}
//
// 		var sw = Stopwatch.StartNew();
// 		await manager.FinishAllAndStopAsync();
// 		sw.Stop();
// 		
// 		double shouldWorkSecond = (countJobs / (countJobs >= parallelJobs ? parallelJobs : 1)) * (oneTimeJobMs / 1000.0 /*in sec*/);
// 		double errorCalc = 1.5;
// 		
// 		Assert.InRange(sw.Elapsed.TotalSeconds, shouldWorkSecond-errorCalc, shouldWorkSecond+errorCalc);
// 		Assert.Equal(0, queue.Count);
// 	}
// 	
// 	[Fact]
// 	public async void TestParallelExecutingJobs_FinishAllAndStopAsync()
// 	{
// 		int countJobs = 5000, oneTimeJobMs = 20, parallelJobs = 50;
// 		var conf = new Config(parallelJobs, TimeSpan.FromSeconds(0.2));
// 		var queue = new MemoryQueue();
//
// 		var manager = new TestQueueReAddFailJobManager(queue, conf, 1);
// 		manager.Start();
//
// 		var jobs = new List<IQueueJob>();
// 		
// 		for (int i = 1; i <= countJobs; i++)
// 		{
// 			var job = new QJob_Ok(i.ToString(), TimeSpan.FromMilliseconds(oneTimeJobMs));
// 			jobs.Add(job);
// 			queue.Enqueue(job);
// 		}
//
// 		await manager.FinishAllAndStopAsync();
// 		double actual = jobs.Sum(x => x.TotalRun);
//
// 		Assert.Equal(5000, actual);
// 		Assert.Equal(0, queue.Count);
// 	}
// 	
// 	[Fact]
// 	public async void TestParallelExecutingJobs_StopAsync()
// 	{
// 		int countJobs = 5000, oneTimeJobMs = 500, parallelJobs = 50;
// 		var conf = new Config(parallelJobs, TimeSpan.FromSeconds(0.2));
// 		var queue = new MemoryQueue();
//
// 		var manager = new TestQueueReAddFailJobManager(queue, conf);
// 		manager.Start();
//
// 		var jobs = new List<IQueueJob>();
// 		var sw = Stopwatch.StartNew();
// 		for (int i = 1; i <= countJobs; i++)
// 		{
// 			var job = new QJob_Ok(i.ToString(), TimeSpan.FromMilliseconds(oneTimeJobMs));
// 			jobs.Add(job);
// 			queue.Enqueue(job);
// 		}
//
// 		await Task.Delay(3000);
// 		manager.Stop();
// 		sw.Stop();
// 		
// 		double actual = jobs.Sum(x => x.TotalRun);
// 		double expected = (sw.Elapsed.TotalMilliseconds / oneTimeJobMs) * parallelJobs; 
// 		double errCalc = 5.0;
// 		
// 		Assert.InRange(actual, expected-errCalc, expected+errCalc);
// 		Assert.InRange(queue.Count, countJobs-expected-errCalc,countJobs-expected+errCalc);
// 	}
// 	
// 	[Fact]
// 	public async void TestParallelExecutingJobs_WithReAdd()
// 	{
// 		int countJobs = 5000, oneTimeJobMs = 20, parallelJobs = 50, att = 2;
// 		var conf = new Config(parallelJobs, TimeSpan.FromSeconds(0.2));
// 		var queue = new MemoryQueue();
//
// 		var manager = new TestQueueReAddFailJobManager(queue, conf, att);
// 		manager.Start();
//
// 		var jobs = new List<IQueueJob>();
// 		
// 		for (int i = 1; i <= countJobs; i++)
// 		{
// 			var job = new QJob_Fail(i.ToString(), TimeSpan.FromMilliseconds(oneTimeJobMs));
// 			jobs.Add(job);
// 			queue.Enqueue(job);
// 		}
//
// 		await manager.FinishAllAndStopAsync();
// 		double actual = jobs.Sum(x => x.TotalRun);
//
// 		Assert.Equal(countJobs*att, actual);
// 		Assert.Equal(0, queue.Count);
// 	}
// 	
// }