﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StbNative;

namespace StbImageSharp.Testing
{
	internal static class Program
	{
		private static int tasksStarted;
		private static int filesProcessed;
		private static int stbSharpLoading;
		private static int stbNativeLoading;

		private const int LoadTries = 10;

		public static void Log(string message)
		{
			Console.WriteLine(Thread.CurrentThread.ManagedThreadId + " -- " + message);
		}

		public static void Log(string format, params object[] args)
		{
			Log(string.Format(format, args));
		}

		private static void BeginWatch(Stopwatch sw)
		{
			sw.Restart();
		}

		private static int EndWatch(Stopwatch sw)
		{
			sw.Stop();
			return (int) sw.ElapsedMilliseconds;
		}

		private delegate byte[] LoadDelegate(out int x, out int y, out ColorComponents comp);

		private static void ParseTest(Stopwatch sw, LoadDelegate load1, LoadDelegate load2,
			out int load1Passed, out int load2Passed)
		{
			Log("With StbSharp");
			int x = 0, y = 0;
			ColorComponents comp = ColorComponents.Default;
			byte[] parsed = new byte[0];
			BeginWatch(sw);

			for (var i = 0; i < LoadTries; ++i)
			{
				parsed = load1(out x, out y, out comp);
			}

			Log("x: {0}, y: {1}, comp: {2}, size: {3}", x, y, comp, parsed.Length);
			var passed = EndWatch(sw)/LoadTries;
			Log("Span: {0} ms", passed);
			load1Passed = passed;

			Log("With Stb.Native");
			int x2 = 0, y2 = 0;
			ColorComponents comp2 = ColorComponents.Default;
			byte[] parsed2 = new byte[0];

			BeginWatch(sw);
			for (var i = 0; i < LoadTries; ++i)
			{
				parsed2 = load2(out x2, out y2, out comp2);
			}
			Log("x: {0}, y: {1}, comp: {2}, size: {3}", x2, y2, comp2, parsed2.Length);
			passed = EndWatch(sw)/LoadTries;
			Log("Span: {0} ms", passed);
			load2Passed = passed;

			if (x != x2)
			{
				throw new Exception(string.Format("Inconsistent x: StbSharp={0}, Stb.Native={1}", x, x2));
			}

			if (y != y2)
			{
				throw new Exception(string.Format("Inconsistent y: StbSharp={0}, Stb.Native={1}", y, y2));
			}

			if (comp != comp2)
			{
				throw new Exception(string.Format("Inconsistent comp: StbSharp={0}, Stb.Native={1}", comp, comp2));
			}

			if (parsed.Length != parsed2.Length)
			{
				throw new Exception(string.Format("Inconsistent parsed length: StbSharp={0}, Stb.Native={1}", parsed.Length,
					parsed2.Length));
			}

			for (var i = 0; i < parsed.Length; ++i)
			{
				if (parsed[i] != parsed2[i])
				{
					throw new Exception(string.Format("Inconsistent data: index={0}, StbSharp={1}, Stb.Native={2}",
						i,
						(int) parsed[i],
						(int) parsed2[i]));
				}
			}
		}

		public static bool RunTests()
		{
			try
			{
				var imagesPath = "..\\..\\..\\TestImages";

				var files = Directory.EnumerateFiles(imagesPath, "*.*", SearchOption.AllDirectories).ToArray();
				Log("Files count: {0}", files.Length);

				foreach (var file in files)
				{
					Task.Factory.StartNew(() => { ThreadProc(file); });
					tasksStarted++;
				}

				while (true)
				{
					Thread.Sleep(1000);

					if (tasksStarted == 0)
					{
						break;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
			}

			return true;
		}

		private static void ThreadProc(string f)
		{
			try
			{
				var sw = new Stopwatch();

				if (!f.EndsWith(".bmp") && !f.EndsWith(".jpg") && !f.EndsWith(".png") &&
					!f.EndsWith(".jpg") && !f.EndsWith(".psd") && !f.EndsWith(".pic") &&
					!f.EndsWith(".tga"))
				{
					return;
				}

				Log(string.Empty);
				Log("{0} -- #{1}: Loading {2} into memory", DateTime.Now.ToLongTimeString(), filesProcessed, f);
				var data = File.ReadAllBytes(f);
				Log("----------------------------");

				Log("Loading");
				int x = 0, y = 0;
				ColorComponents comp = ColorComponents.Default;
				int stbSharpPassed, stbNativePassed;
				byte[] parsed = new byte[0];
				ParseTest(
					sw,
					(out int xx, out int yy, out ColorComponents ccomp) =>
					{
						var img = Image.FromMemory(data);

						parsed = img.Data;
						xx = img.Width;
						yy = img.Height;
						ccomp = img.SourceComp;

						x = xx;
						y = yy;
						comp = ccomp;
						return parsed;
					},
					(out int xx, out int yy, out ColorComponents ccomp) =>
					{
						int c;
						var result = Native.load_from_memory(data, out xx, out yy, out c, 0);
						ccomp = (ColorComponents)c;
						return result;
					},
					out stbSharpPassed, out stbNativePassed
				);
				stbSharpLoading += stbSharpPassed;
				stbNativeLoading += stbNativePassed;

				Log("Total StbSharp Loading Time: {0} ms", stbSharpLoading);
				Log("Total Stb.Native Loading Time: {0} ms", stbNativeLoading);

				Log("Native Memory Allocations not freed: " + MemoryStats.Allocations);
				Log("GC Memory: {0}", GC.GetTotalMemory(true));

				++filesProcessed;
				Log(DateTime.Now.ToLongTimeString() + " -- " + " Files processed: " + filesProcessed);

			}
			catch (Exception ex)
			{
				Log("Error: " + ex.Message);
			}
			finally
			{
				--tasksStarted;
			}
		}

		public static int Main(string[] args)
		{
			var start = DateTime.Now;

			var res = RunTests();
			var passed = DateTime.Now - start;
			Log("Span: {0} ms", passed.TotalMilliseconds);
			Log(DateTime.Now.ToLongTimeString() + " -- " + (res ? "Success" : "Failure"));

			return res ? 1 : 0;
		}
	}
}