﻿
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Tasks;
using Open.HttpProxy.Utils;

namespace Open.HttpProxy
{
	using BufferManager;
	using EventArgs;
	using Listeners;
	
	public class HttpProxy
	{
		private readonly TcpListener _listener;
		public static EventHandler<ConnectionEventArgs> OnClientConnect;

		public EventHandler<SessionEventArgs> OnRequestHeaders;
		public EventHandler<SessionEventArgs> OnRequest;
		public EventHandler<SessionEventArgs> OnResponseHeaders;
		public EventHandler<SessionEventArgs> OnResponse;
		internal static Logger Logger = new Logger();
		internal static readonly BufferAllocator BufferAllocator = new BufferAllocator(new byte[20 * 1024 * 1024]);

		public HttpProxy(int port=8888)
		{
			_listener = new TcpListener(port);
			_listener.ConnectionRequested += OnConnectionRequested;
		}

		public void Start()
		{
			_listener.Start();
		}

		public void Stop()
		{
			_listener.Stop();
		}

		private void OnConnectionRequested(object sender, ConnectionEventArgs e)
		{
			Task.Run(async () => await HandleSession(e));
		}

		private async Task HandleSession(ConnectionEventArgs e)
		{
			using (Logger.Enter($"Receiving new connection from: {e.Socket.RemoteEndPoint}"))
			{
				var clientConnection = e.Stream;

				Events.Raise(OnClientConnect, this, e);
				var session = new Session(clientConnection, _listener.Endpoint, this);
				try
				{
					await StateMachine.RunAsync(session).WithoutCapturingContext();
				}
				catch (Exception ex)
				{
					Logger.LogData(TraceEventType.Error, ex);
					await session.ClientHandler.SendErrorAsync(
						ProtocolVersion.Parse("HTTP/1.1"), 
						HttpStatusCode.BadGateway, "Bad Gateway", ex.ToString())
						.WithoutCapturingContext();
				}
				finally
				{
					((NetworkStream) clientConnection)?.Close(1);
				}
			}
		}
	}
}
