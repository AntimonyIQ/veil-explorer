using Npgsql;
using ExplorerBackend.Models.Data;
using ExplorerBackend.Services.Core;

namespace ExplorerBackend.Persistence.Repositories;

public class TransactionsRepository : BaseRepository, ITransactionsRepository
{
    public TransactionsRepository(IConfiguration config, IUtilityService utilityService) : base(config, utilityService) { }

    protected async Task<Transaction?> ReadTransaction(NpgsqlDataReader reader)
    {
        var tx = new Transaction();

        tx.txid_hex = await ReadHexFromBytea(reader, 0);
        tx.hash_hex = await ReadHexFromBytea(reader, 1);
        tx.version = reader.GetInt32(2);
        tx.size = reader.GetInt32(3);
        tx.vsize = reader.GetInt32(4);
        tx.weight = reader.GetInt32(5);
        tx.locktime = reader.GetInt64(6);
        tx.block_height = reader.GetInt32(7);

        return tx;
    }

    public async Task<Transaction?> GetTransactionByIdAsync(string txid)
    {
        using var conn = Connection;
        await conn.OpenAsync();

        using (var cmd = new NpgsqlCommand($"SELECT * FROM transactions WHERE txid = {TransformHex(txid)}", conn))
        {
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                var success = await reader.ReadAsync();
                if (!success) return null;

                return await ReadTransaction(reader);
            }
        }
    }


    public async Task<TransactionExtended?> GetTransactionFullByIdAsync(string txid)
    {
        using var conn = Connection;
        await conn.OpenAsync();

        using (var cmd = new NpgsqlCommand($@"SELECT t.txid, t.hash, t.""version"", t.""size"", t.vsize, t.weight, t.locktime, t.block_height, r.""data"" FROM transactions as t INNER JOIN rawtxs r ON t.txid = r.txid  WHERE t.txid = {TransformHex(txid)};", conn))
        {
            await using (var reader = await cmd.ExecuteReaderAsync())
            {

                var success = await reader.ReadAsync();
                if (!success) return null;
                var rt = await ReadTransaction(reader);
                if (rt != null)
                {
                    var tx = new TransactionExtended
                    {
                        txid_hex = rt.txid_hex,
                        hash_hex = rt.hash_hex,
                        version = rt.version,
                        size = rt.size,
                        vsize = rt.vsize,
                        weight = rt.weight,
                        locktime = rt.locktime,
                        block_height = rt.block_height
                    };
                    tx.data = await ReadBytea(reader, 8);
                    return tx;
                }


                return null;
            }
        }
    }

    public async Task<List<TransactionExtended>?> GetTransactionsForBlockAsync(int blockHeight, int offset, int count)
    {
        using var conn = Connection;
        await conn.OpenAsync();

        using (var cmd = new NpgsqlCommand($@"SELECT t.txid, t.hash, t.""version"", t.""size"", t.vsize, t.weight, t.locktime, t.block_height, r.""data"" FROM transactions as t INNER JOIN rawtxs r ON t.txid = r.txid  WHERE t.block_height = {blockHeight} OFFSET {offset} LIMIT {count};", conn))
        {
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                var txs = new List<TransactionExtended>();

                while (await reader.ReadAsync())
                {
                    var rt = await ReadTransaction(reader);
                    if (rt != null)
                    {
                        var tx = new TransactionExtended
                        {
                            txid_hex = rt.txid_hex,
                            hash_hex = rt.hash_hex,
                            version = rt.version,
                            size = rt.size,
                            vsize = rt.vsize,
                            weight = rt.weight,
                            locktime = rt.locktime,
                            block_height = rt.block_height
                        };
                        tx.data = await ReadBytea(reader, 8);
                        txs.Add(tx);
                    }
                }

                return txs;
            }
        }
    }

    public async Task<string?> ProbeTransactionByHashAsync(string txid)
    {
        using var conn = Connection;
        await conn.OpenAsync();

        using (var cmd = new NpgsqlCommand($"SELECT txid FROM transactions WHERE txid = {TransformHex(txid)}", conn))
        {
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                var success = await reader.ReadAsync();
                if (!success) return null;

                return await ReadHexFromBytea(reader, 0);
            }
        }
    }

    public async Task<bool> InsertTransactionAsync(Transaction txTemplate)
    {
        using var conn = Connection;
        await conn.OpenAsync();

        using (var cmd = new NpgsqlCommand(@"INSERT INTO transactions (txid,hash,""version"",""size"",vsize,weight,locktime,block_height) VALUES (" +
                                            $"{TransformHex(txTemplate.txid_hex)}, {TransformHex(txTemplate.hash_hex)}, {txTemplate.version}, {txTemplate.size}, {txTemplate.vsize}, {txTemplate.weight}, {txTemplate.locktime}, {txTemplate.block_height});", conn))
        {
            await cmd.PrepareAsync();
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}