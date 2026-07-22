using System;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;

namespace SistemaInventario.Api.Infrastructure.Security
{
    public sealed class BlockchainLoggerProvider : ILoggerProvider
    {
        private readonly BlockchainLoggerOptions _options;
        private readonly Web3? _web3;
        private readonly Account? _account;

        public BlockchainLoggerProvider(IConfiguration configuration)
        {
            _options = configuration.GetSection("BlockchainLogging").Get<BlockchainLoggerOptions>() ?? new BlockchainLoggerOptions();

            if (string.IsNullOrWhiteSpace(_options.PrivateKey))
            {
                var ecKey = EthECKey.GenerateKey();
                _options.PrivateKey = ecKey.GetPrivateKey();
                var tempAddress = ecKey.GetPublicAddress();
                Console.WriteLine("[BlockchainLogger] No PrivateKey configured. Generated temporary key pair.");
                Console.WriteLine($"[BlockchainLogger] Generated Address: {tempAddress}");
                Console.WriteLine($"[BlockchainLogger] Generated PrivateKey: {_options.PrivateKey}");
            }

            _account = new Account(_options.PrivateKey.Trim());
            _options.RpcUrl ??= "https://ethereum-sepolia-rpc.publicnode.com";
            _web3 = new Web3(_account, _options.RpcUrl);
            Console.WriteLine($"[BlockchainLogger] Using blockchain account: {_account.Address}");
            Console.WriteLine($"[BlockchainLogger] RPC URL: {_options.RpcUrl}");
        }

        public ILogger CreateLogger(string categoryName)
            => new BlockchainLogger(categoryName, _web3, _account, _options);

        public void Dispose() { }
    }

    internal sealed class BlockchainLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly Web3? _web3;
        private readonly Account? _account;
        private readonly BlockchainLoggerOptions _options;

        public BlockchainLogger(string categoryName, Web3? web3, Account? account, BlockchainLoggerOptions options)
        {
            _categoryName = categoryName;
            _web3 = web3;
            _account = account;
            _options = options;
        }

        IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _options.MinimumLogLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel) || formatter == null)
            {
                return;
            }

            if (_web3 is null || _account is null)
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var payload = BuildPayload(logLevel, eventId, message, exception);

            _ = Task.Run(async () =>
            {
                try
                {
                    await SendLogAsync(payload).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BlockchainLogger] Error sending blockchain log: {ex.Message}");
                    Console.WriteLine(ex);
                }
            });
        }

        private string BuildPayload(LogLevel logLevel, EventId eventId, string message, Exception? exception)
        {
            var builder = new StringBuilder();
            builder.Append($"[{DateTime.UtcNow:O}] ");
            builder.Append($"[{logLevel}] ");
            builder.Append($"[{_categoryName}] ");

            if (eventId.Id != 0 || !string.IsNullOrWhiteSpace(eventId.Name))
            {
                builder.Append($"[{eventId.Id}:{eventId.Name}] ");
            }

            builder.Append(message);

            if (exception is not null)
            {
                builder.Append(" | Exception: ");
                builder.Append(exception.GetType().FullName);
                builder.Append(": ");
                builder.Append(exception.Message);
            }

            return builder.ToString();
        }

        private async Task SendLogAsync(string payload)
        {
            if (_web3 is null || _account is null)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(payload);
            var data = "0x" + bytes.ToHex();

            var balance = await _web3.Eth.GetBalance.SendRequestAsync(_account.Address).ConfigureAwait(false);
            var gasPrice = new HexBigInteger(1_000_000_000);
            var gasLimit = new HexBigInteger(_options.GasLimit);
            var value = new HexBigInteger(0);
            var txCost = gasPrice.Value * gasLimit.Value;

            if (balance.Value == 0)
            {
                Console.WriteLine($"[BlockchainLogger] Cannot send blockchain log: account {_account.Address} has zero balance. Fund it with Sepolia ETH.");
                return;
            }

            if (balance.Value < txCost)
            {
                Console.WriteLine($"[BlockchainLogger] Cannot send blockchain log: insufficient balance {balance.Value} wei for tx cost {txCost} wei. Fund account with Sepolia ETH.");
                return;
            }

            var txInput = new TransactionInput(data, _account.Address, _account.Address, gasLimit, gasPrice, value);
            Console.WriteLine($"[BlockchainLogger] Submitting blockchain log transaction from {_account.Address}...");
            var txHash = await _web3.Eth.TransactionManager.SendTransactionAsync(txInput).ConfigureAwait(false);
            Console.WriteLine($"[BlockchainLogger] Blockchain log transaction submitted: {txHash}");
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    public sealed class BlockchainLoggerOptions
    {
        public string? PrivateKey { get; set; }
        public string? RpcUrl { get; set; } = "https://ethereum-sepolia-rpc.publicnode.com";
        public int GasLimit { get; set; } = 21000;
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    }
}
