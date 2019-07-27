// <copyright file="PausableFtpService.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace FubarDev.FtpServer.Networking
{
    /// <summary>
    /// Base class for communication services.
    /// </summary>
    internal abstract class PausableFtpService : IPausableFtpService
    {
        private readonly CancellationTokenSource _jobStopped = new CancellationTokenSource();

        private readonly CancellationToken _connectionClosed;
        private CancellationTokenSource _jobPaused = new CancellationTokenSource();
        private Task _task = Task.CompletedTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="PausableFtpService"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="connectionClosed">Cancellation token source for a closed connection.</param>
        protected PausableFtpService(
            CancellationToken connectionClosed,
            ILogger? logger = null)
        {
            Logger = logger;
            _connectionClosed = connectionClosed;
        }

        /// <inheritdoc />
        public FtpServiceStatus Status { get; private set; } = FtpServiceStatus.ReadyToRun;

        /// <summary>
        /// Gets the logger.
        /// </summary>
        protected ILogger? Logger { get; }

        /// <summary>
        /// Gets a value indicating whether the connection is closed.
        /// </summary>
        protected bool IsConnectionClosed => _connectionClosed.IsCancellationRequested;

        /// <summary>
        /// Gets a value indicating whether stopping the connection was requested.
        /// </summary>
        protected bool IsStopRequested => _jobStopped.IsCancellationRequested;

        /// <summary>
        /// Gets a value indicating whether pausing the connection was requested.
        /// </summary>
        protected bool IsPauseRequested => _jobPaused.IsCancellationRequested;

        /// <inheritdoc />
        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (Status != FtpServiceStatus.ReadyToRun)
            {
                throw new InvalidOperationException($"Status must be {FtpServiceStatus.ReadyToRun}, but was {Status}.");
            }

            _jobPaused = new CancellationTokenSource();
            _task = RunAsync(new Progress<FtpServiceStatus>(status => Status = status));

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (Status != FtpServiceStatus.Running && Status != FtpServiceStatus.Stopped && Status != FtpServiceStatus.Paused)
            {
                throw new InvalidOperationException($"Status must be {FtpServiceStatus.Running}, {FtpServiceStatus.Stopped}, or {FtpServiceStatus.Paused}, but was {Status}.");
            }

            await OnStopRequestingAsync(cancellationToken)
               .ConfigureAwait(false);

            _jobStopped.Cancel();

            await OnStopRequestedAsync(cancellationToken)
               .ConfigureAwait(false);

            await _task
               .ConfigureAwait(false);

            Status = FtpServiceStatus.Stopped;
        }

        /// <inheritdoc />
        public async Task PauseAsync(CancellationToken cancellationToken)
        {
            if (Status == FtpServiceStatus.Paused)
            {
                return;
            }

            if (Status != FtpServiceStatus.Running)
            {
                throw new InvalidOperationException($"Status must be {FtpServiceStatus.Running}, but was {Status}.");
            }

            await OnPauseRequestingAsync(cancellationToken)
               .ConfigureAwait(false);

            _jobPaused.Cancel();

            await OnPauseRequestedAsync(cancellationToken)
               .ConfigureAwait(false);

            await _task
               .ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ContinueAsync(CancellationToken cancellationToken)
        {
            if (Status == FtpServiceStatus.Stopped)
            {
                // Stay stopped!
                return;
            }

            if (Status == FtpServiceStatus.Running)
            {
                // Already running!
                return;
            }

            if (Status != FtpServiceStatus.Paused)
            {
                throw new InvalidOperationException($"Status must be {FtpServiceStatus.Paused}, but was {Status}.");
            }

            _jobPaused = new CancellationTokenSource();

            await OnContinueRequestingAsync(cancellationToken)
               .ConfigureAwait(false);

            _task = RunAsync(new Progress<FtpServiceStatus>(status => Status = status));
        }

        /// <summary>
        /// Execute the task this service needs to execute.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task doing its job.</returns>
        protected abstract Task ExecuteAsync(
            CancellationToken cancellationToken);

        /// <summary>
        /// Gets called when stopping the service is about to be requested.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task.</returns>
        protected virtual Task OnStopRequestingAsync(
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets called when stopping the service was requested.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task.</returns>
        protected virtual Task OnStopRequestedAsync(
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets called when the pause is about to be requested.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task.</returns>
        protected virtual Task OnPauseRequestingAsync(
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets called when the pause was requested.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task.</returns>
        protected virtual Task OnPauseRequestedAsync(
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets called when the continuation request is about to be issued.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task.</returns>
        protected virtual Task OnContinueRequestingAsync(
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets called when the service was paused.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task.</returns>
        protected virtual Task OnPausedAsync(
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets called when the service was stopped.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task.</returns>
        protected virtual Task OnStoppedAsync(
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets called when the service failed.
        /// </summary>
        /// <param name="exception">The exception for the failure.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task returning <see langword="true"/>, when the failure was handled and shouldn't be rethrown.</returns>
        protected virtual Task<bool> OnFailedAsync(
            Exception exception,
            CancellationToken cancellationToken)
        {
            Logger?.LogCritical(exception, exception.Message);
            return Task.FromResult(false);
        }

        private async Task RunAsync(
            IProgress<FtpServiceStatus> statusProgress)
        {
            using (var globalCts = CancellationTokenSource.CreateLinkedTokenSource(
                _connectionClosed,
                _jobStopped.Token,
                _jobPaused.Token))
            {
                statusProgress.Report(FtpServiceStatus.Running);

                try
                {
                    await ExecuteAsync(globalCts.Token)
                       .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex.Is<OperationCanceledException>())
                {
                    // Ignore - everything is fine
                    // Logger?.LogTrace("Operation cancelled");
                }
                catch (Exception ex) when (ex.Is<IOException>())
                {
                    // Ignore - everything is fine
                    // Logger?.LogTrace(0, ex, "I/O exception: {message}", ex.Message);
                }
                catch (Exception ex)
                {
                    Logger?.LogTrace(0, ex, "Failed: {message}", ex.Message);
                    var isHandled = await OnFailedAsync(ex, _connectionClosed)
                       .ConfigureAwait(false);

                    if (!isHandled)
                    {
                        throw;
                    }
                }
            }

            // Don't call Complete() when the job was just paused.
            if (IsPauseRequested)
            {
                statusProgress.Report(FtpServiceStatus.Paused);
                await OnPausedAsync(_connectionClosed)
                   .ConfigureAwait(false);
                return;
            }

            // Change the status
            statusProgress.Report(FtpServiceStatus.Stopped);
            await OnStoppedAsync(_connectionClosed)
               .ConfigureAwait(false);
        }
    }
}
