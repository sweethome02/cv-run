using Microsoft.Data.Sqlite;

namespace ClipNestWpf;

public class Storage : IDisposable
{
    readonly SqliteConnection _db;

    public Storage(string path)
    {
        _db = new SqliteConnection($"Data Source={path}");
        _db.Open();

        // Create table (new DB) or migrate (old DB)
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

        // Migration: add format column for DBs created before v2
        try
        {
            using var migrate = _db.CreateCommand();
            migrate.CommandText = "ALTER TABLE items ADD COLUMN format TEXT DEFAULT 'png'";
            migrate.ExecuteNonQuery();
        }
        catch
        {
            // Column already exists — migration is a no-op
        }

        // Migration: add name column for DBs created before v3
        try
        {
            using var migrate = _db.CreateCommand();
            migrate.CommandText = "ALTER TABLE items ADD COLUMN name TEXT DEFAULT ''";
            migrate.ExecuteNonQuery();
        }
        catch { }
    }

    public void Save(ClipboardItem item)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO items VALUES (@id,@type,@fmt,@content,@pin,@ts,@name)";
        cmd.Parameters.AddWithValue("@id", item.Id);
        cmd.Parameters.AddWithValue("@type", item.Type);
        cmd.Parameters.AddWithValue("@fmt", item.Format);
        cmd.Parameters.AddWithValue("@content", item.Content);
        cmd.Parameters.AddWithValue("@pin", item.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@ts", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        cmd.Parameters.AddWithValue("@name", item.Name);
        cmd.ExecuteNonQuery();
    }

    public List<ClipboardItem> List(int limit = 200)
    {
        var items = new List<ClipboardItem>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id,type,format,content,name,is_pinned,created_at FROM items ORDER BY created_at DESC LIMIT @n";
        cmd.Parameters.AddWithValue("@n", limit);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(new ClipboardItem
            {
                Id = r.GetString(0),
                Type = r.GetString(1),
                Format = r.GetString(2),
                Content = r.GetString(3),
                Name = r.IsDBNull(4) ? "" : r.GetString(4),
                IsPinned = r.GetInt32(5) != 0,
                CreatedAt = r.GetInt64(6)
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
