﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Text;
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

		public static string[] CommandLine = null;

		public static void Start()
		{
			Configure();
			new Thread(ServerProc).Start(null);
		}

		private static void Configure()
		{
			LoadConfigFile();
			LoadCommandLine();
			SaveConfigFile();
		}

		private static void SaveConfigFile()
		{
			var fileName = GetConfigFileName();
			try
			{
				var sb = new StringBuilder();
				sb.AppendLine(string.Format("IncomeAddress={0}", IncomeAddress));
				sb.AppendLine(string.Format("IncomePort={0}", IncomePort));
				sb.AppendLine(string.Format("RdpAddress={0}", RdpAddress));
				sb.AppendLine(string.Format("RdpPort={0}", RdpPort));
				sb.AppendLine(string.Format("OtherAddress={0}", OtherAddress));
				sb.AppendLine(string.Format("OtherPort={0}", OtherPort));
				File.WriteAllText(fileName, sb.ToString());
			}
			catch (Exception e)
			{
				
			}
		}

		protected static string[] RefKeys = new[] {"IA", "IP", "RA", "RP", "OA", "OP"};
		protected static string[] RefVals = new[] { "INCOMEADDRESS", "INCOMEPORT", "RDPADDRESS",
			"RDPPORT", "OTHERADDRESS", "OTHERPORT"};

		private static void LoadCommandLine()
		{
			if (CommandLine == null || CommandLine.Length==0) return;
			for (int idx = 0; idx < CommandLine.Length; idx++)
			{
				try
				{
					var arg = CommandLine[idx].Trim().ToUpper();
					if (!arg.StartsWith("-") && !arg.StartsWith("/")) continue;
					arg = arg.Substring(1);
					var n = Array.IndexOf(RefKeys, arg);
					if (n == -1) n = Array.IndexOf(RefVals, arg);
					if (n == -1) continue;
					var value = idx + 1 < CommandLine.Length ? CommandLine[idx + 1] : "";
					ParseConfigPair(RefVals[n], value);
					idx++;
				}
				catch (Exception e)
				{
					Error(e, "on parsing command line <{0}>", CommandLine[idx]);
				}
			}
		}

		public static void LoadConfigFile()
		{
			var fileName = GetConfigFileName();
			if (!File.Exists(fileName)) return;
			try
			{
				var lines = File.ReadAllLines(fileName);
				foreach (var line in lines)
				{
					if (string.IsNullOrEmpty(line)) continue;
					var parts = line.Split(new[] {'='}, 2);
					if (parts.Length != 2) continue;
					var key = parts[0].Trim().ToUpper();
					var value = parts[1].Trim();
					ParseConfigPair(key, value);
				}
			}
			catch (Exception e)
			{
				Error(e, "on reading config file <{0}>", fileName);
			}
		}

		private static void ParseConfigPair(string key, string value)
		{
			try
			{
				if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value)) return;
				if (key == "INCOMEADDRESS") IncomeAddress = value;
				if (key == "INCOMEPORT") IncomePort = int.Parse(value);
				if (key == "RDPADDRESS") RdpAddress = value;
				if (key == "RDPPORT") RdpPort = int.Parse(value);
				if (key == "OTHERADDRESS") OtherAddress = value;
				if (key == "OTHERPORT") OtherPort = int.Parse(value);
			}
			catch (Exception e)
			{
				Error(e, "on parsing config {0} value {1}", key, value);
			}
		}

		private static void Info(string format, params object[] args)
		{
			if (args != null && args.Length > 0) format = string.Format(format, args);
			var message = string.Format("info - {0}", format);
			Debug.WriteLine(message);
			if (Environment.UserInteractive) Console.WriteLine(message);
		}

		private static void Error(Exception exc, string format, params object[] args)
		{
			if (args != null && args.Length > 0) format = string.Format(format, args);
			var message = string.Format("error - {0} exception: {1}", format, exc);
			Debug.WriteLine(message);
			if (Environment.UserInteractive) Console.WriteLine(message);
		}

		private static string GetConfigFileName()
		{
			var loc = Assembly.GetExecutingAssembly().Location;
			return Path.ChangeExtension(loc, ".cfg");
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
				var socket = listener.EndAccept(ar);
				Info("new connection from {0}", socket.RemoteEndPoint);
				thread.Start(socket);
			}
			catch (Exception e)
			{
				if (listener != null) listener.Close();
				Error(e, "on incoming connection");
			}
			finally
			{
				NextFlag.Set();
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