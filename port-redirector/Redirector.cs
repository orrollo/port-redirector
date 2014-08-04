using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace port_redirector
{
	class Redirector
	{
		private static readonly ManualResetEvent StopFlag = new ManualResetEvent(false);
		private static readonly ManualResetEvent NextFlag = new ManualResetEvent(true);
		
		public static int IncomePort = 2525;
		public static string LocalIp = "0.0.0.0";

		public static void Start()
		{
			new Thread(ServerProc).Start(null);
		}

		private static void ServerProc(object x)
		{
			var endPoint = new IPEndPoint(IPAddress.Parse(LocalIp), IncomePort);
			var listener = new Socket(endPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			listener.Bind(endPoint);
			listener.Listen(10);
			while (!StopFlag.WaitOne(0))
			{
				if (NextFlag.WaitOne(0))
				{
					NextFlag.Reset();
					listener.BeginAccept(OnIncome, listener);
				}
				else Thread.Sleep(50);
			}
			listener.Close();
			Thread.Sleep(1500);
		}

		public static void Stop()
		{
			StopFlag.Set();
		}

		private static void OnIncome(IAsyncResult ar)
		{
			Socket listener = null;
			try
			{
				listener = (Socket) ar.AsyncState;
				var thread = new Thread(ForwardingProcedure);
				thread.Start(listener.EndAccept(ar));
				NextFlag.Set();
			}
			catch (Exception e)
			{
				if (listener!=null) listener.Close();
			}
		}

		private static void ForwardingProcedure(object obj)
		{
			var input = (Socket)obj;
			var output = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			try
			{
				var buf = new byte[128 * 1024];
				ConnectByInputData(input, output);
				while (!StopFlag.WaitOne(0))
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
				if (StopFlag.WaitOne(0)) break;
				var len = input.Receive(buf);
				if (len <= 0) continue;
				ret = true;
				for (int idx = 0; idx < len; )
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