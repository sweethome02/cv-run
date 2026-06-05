using Microsoft.Data.Sqlite;

namespace ClipNestWpf;

public class Storage : IDisposable
{
    readonly SqliteConnection _db;

    public Storage(string path)
    {
        _db = new SqliteConnection($"Data Source={path}");
        _db.Open();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS items (
                id TEXT PRIMARY KEY,
                type TEXT NOT NULL,
                format TEXT DEFAULT 'png',
                content TEXT NOT NULL,
                is_pinned INTEGER DEFAULT 0,
                created_at INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_items_time ON items(created_at DESC);
            """;
        cmd.ExecuteNonQuery();
    }

    public void Save(ClipboardItem item)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO items VALUES (@id,@type,@fmt,@content,@pin,@ts)";
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@type", item.Type);
        cmd.Parameters.AddWithValue("@fmt", item.Format);
        cmd.Parameters.AddWithValue("@content", item.Content);
        cmd.Parameters.AddWithValue("@pin", item.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.ExecuteNonQuery();
    }

    public List<ClipboardItem> List(int limit = 200)
    {
        var items = new List<ClipboardItem>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id,type,format,content,is_pinned,created_at FROM items ORDER BY created_at DESC LIMIT @n";
        cmd.Parameters.AddWithValue("@n", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(new ClipboardItem
            {
                Id = r.GetString(0),
                Type = r.GetString(1),
                Format = r.GetString(2),
                Content = r.GetString(3),
                IsPinned = r.GetInt32(4) != 0,
                CreatedAt = r.GetInt64(5)
            });
        return items;
    }

    public void TogglePin(string id, bool pinned)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE items SET is_pinned=@p WHERE id=@id";
        cmd.Parameters.AddWithValue("@p", pinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void Delete(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void ClearTemp()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM items WHERE is_pinned=0";
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _db.Dispose();
}
