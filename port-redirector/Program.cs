using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace port_redirector
{
	class Program
	{
		static readonly ManualResetEvent Stop = new ManualResetEvent(false);
		static readonly ManualResetEvent Next = new ManualResetEvent(true);

		static void Main(string[] args)
		{
			Console.CancelKeyPress += Console_CancelKeyPress;

			var endPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 2525);
			var listener = new Socket(endPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			listener.Bind(endPoint);
			listener.Listen(10);
			while (!Stop.WaitOne(0))
			{
				if (Next.WaitOne(0))
				{
					Next.Reset();
					listener.BeginAccept(OnIncome, listener);
				}
				else Thread.Sleep(50);
			}
			listener.Close();
			Thread.Sleep(1500);
		}

		private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			Stop.Set();
		}

		private static void OnIncome(IAsyncResult ar)
		{
			var listener = (Socket) ar.AsyncState;
			var thread = new Thread(ForwardingProcedure);
			thread.Start(listener.EndAccept(ar));
			Next.Set();
		}

		private static void ForwardingProcedure(object obj)
		{
			var input = (Socket) obj;
			var output = new Socket(AddressFamily.InterNetwork,SocketType.Stream, ProtocolType.Tcp);
			try
			{
				var buf = new byte[128 * 1024];
				ConnectByInputData(input, output);
				while (!Stop.WaitOne(0))
				{
					if (Transfer(input, output, buf)) continue;
					if (Transfer(output, input, buf)) continue;
					Thread.Sleep(10);
				}
			}
			catch (Exception e)
			{
				input.Close();
				output.Close();
			}
		}

		private static bool Transfer(Socket input, Socket output, byte[] buf)
		{
			var ret = false;
			while (input.Available > 0)
			{
				if (Stop.WaitOne(0)) break;
				var len = input.Receive(buf);
				if (len <= 0) continue;
				ret = true;
				for (int idx = 0; idx < len;) 
					idx += output.Send(buf, idx, len - idx, SocketFlags.None);
			}
			return ret;
		}

		// dumb, for example
		private static void ConnectByInputData(Socket input, Socket output)
		{
			output.Connect("www.google.com", 80);
		}
	}
}
