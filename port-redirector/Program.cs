using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace port_redirector
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.CancelKeyPress += Console_CancelKeyPress;
			Redirector.Start();
			Console.ReadLine();
			DoStop();
		}

		private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			DoStop();
		}

		private static void DoStop()
		{
			Redirector.Stop();
			Thread.Sleep(1500);
		}
	}
}
