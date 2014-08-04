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

		public static string IncomeAddress = "0.0.0.0";
		public static int IncomePort = 2525;

		public static string OtherAddress = "www.google.com";
		public static int OtherPort = 80;
	
		public static string RdpAddress = "127.0.0.1";
		public static int RdpPort = 3389;

		public static void Start()
		{
			new Thread(ServerProc).Start(null);
		}

		private static void ServerProc(object x)
		{
			var endPoint = new IPEndPoint(IPAddress.Parse(IncomeAddress), IncomePort);
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
				ConnectByInputData(input, output, buf);
				while (!StopFlag.WaitOne(0))
				{
					if (Transfer(input, output, buf)) continue;
					if (Transfer(output, input, buf)) continue;
					Thread.Sleep(10);
				}
			}
			catch (Exception e)
			{
				if (input.Connected) input.Disconnect(false);
				if (output.Connected) output.Disconnect(false);
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
				SendBuf(output, buf, len);
			}
			return ret;
		}

		

		private static void ConnectByInputData(Socket input, Socket output, byte[] buf)
		{
			var isRdp = false;
			var len = 0;
			for (var cnt = 0; cnt < 20; cnt++)
			{
				if (input.Available <= 0) Thread.Sleep(50);
				else
				{
					len = input.Receive(buf);
					isRdp = (buf.Length > 3) && (buf[0] == 3) && (((buf[2]*256 + buf[3]) == len));
					break;
				}
			}
			if (isRdp) ConnectRdp(output); else ConnectOther(output);
			if (len > 0) SendBuf(output, buf, len);
		}

		private static void ConnectOther(Socket output)
		{
			output.Connect(OtherAddress, OtherPort);
		}

		private static void ConnectRdp(Socket output)
		{
			output.Connect(RdpAddress, RdpPort);
		}

		private static void SendBuf(Socket output, byte[] buf, int len)
		{
			for (var idx = 0; idx < len;)
				idx += output.Send(buf, idx, len - idx, SocketFlags.None);
		}
	}
}