namespace ForzaStudio
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    // SQLite database held entirely in memory, through Windows' own SQLite (winsqlite3.dll, Windows 10
    // and later), so nothing has to be shipped and no journal or temporary file is ever written.
    // Values come back as long, double, string or null.
    public sealed class SqliteDatabase : IDisposable
    {
        private const string Library = "winsqlite3.dll";
        private const int SQLITE_OK = 0;
        private const int SQLITE_ROW = 100;
        private const int SQLITE_DONE = 101;
        private const int SQLITE_INTEGER = 1;
        private const int SQLITE_FLOAT = 2;
        private const int SQLITE_NULL = 5;
        private const int OpenReadWriteCreate = 0x02 | 0x04;
        private const int OpenMemory = 0x80;
        private const uint DeserializeFreeOnClose = 1;
        private const uint DeserializeResizeable = 2;
        private static readonly IntPtr Transient = new IntPtr(-1);

        [DllImport(Library)] private static extern int sqlite3_open_v2(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Library)] private static extern int sqlite3_close(IntPtr db);
        [DllImport(Library)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(Library)] private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int length, out IntPtr statement, IntPtr tail);
        [DllImport(Library)] private static extern int sqlite3_step(IntPtr statement);
        [DllImport(Library)] private static extern int sqlite3_finalize(IntPtr statement);
        [DllImport(Library)] private static extern int sqlite3_column_count(IntPtr statement);
        [DllImport(Library)] private static extern IntPtr sqlite3_column_name(IntPtr statement, int column);
        [DllImport(Library)] private static extern int sqlite3_column_type(IntPtr statement, int column);
        [DllImport(Library)] private static extern long sqlite3_column_int64(IntPtr statement, int column);
        [DllImport(Library)] private static extern double sqlite3_column_double(IntPtr statement, int column);
        [DllImport(Library)] private static extern IntPtr sqlite3_column_text(IntPtr statement, int column);
        [DllImport(Library)] private static extern int sqlite3_column_bytes(IntPtr statement, int column);
        [DllImport(Library)] private static extern int sqlite3_bind_int64(IntPtr statement, int index, long value);
        [DllImport(Library)] private static extern int sqlite3_bind_double(IntPtr statement, int index, double value);
        [DllImport(Library)] private static extern int sqlite3_bind_text(IntPtr statement, int index, byte[] value, int length, IntPtr destructor);
        [DllImport(Library)] private static extern int sqlite3_bind_null(IntPtr statement, int index);
        [DllImport(Library)] private static extern int sqlite3_changes(IntPtr db);
        [DllImport(Library)] private static extern long sqlite3_last_insert_rowid(IntPtr db);
        [DllImport(Library)] private static extern IntPtr sqlite3_malloc64(long size);
        [DllImport(Library)] private static extern void sqlite3_free(IntPtr memory);
        [DllImport(Library)] private static extern int sqlite3_deserialize(IntPtr db, byte[] schema, IntPtr data, long size, long bufferSize, uint flags);
        [DllImport(Library)] private static extern IntPtr sqlite3_serialize(IntPtr db, byte[] schema, out long size, uint flags);

        public class Result
        {
            public string[] Columns;
            public List<object[]> Rows = new List<object[]>();
        }

        private IntPtr db;

        private SqliteDatabase()
        {
        }

        public static SqliteDatabase FromBytes(byte[] data)
        {
            SqliteDatabase database = new SqliteDatabase();
            try
            {
                if (sqlite3_open_v2(Utf8(":memory:"), out database.db, OpenReadWriteCreate | OpenMemory, IntPtr.Zero) != SQLITE_OK)
                {
                    throw new InvalidOperationException("Cannot create the in-memory database.");
                }
            }
            catch (DllNotFoundException)
            {
                throw new NotSupportedException("The database editor needs Windows' SQLite (winsqlite3.dll, Windows 10 or later).");
            }
            IntPtr memory = sqlite3_malloc64(Math.Max(data.Length, 1));
            if (memory == IntPtr.Zero)
            {
                database.Dispose();
                throw new InvalidOperationException(NoMemoryMessage(data.Length));
            }
            Marshal.Copy(data, 0, memory, data.Length);
            int rc = sqlite3_deserialize(database.db, Utf8("main"), memory, data.Length, data.Length, DeserializeFreeOnClose | DeserializeResizeable);
            if (rc != SQLITE_OK)
            {
                string message = database.Error;
                database.Dispose();
                if (rc == SQLITE_NOMEM)
                {
                    throw new InvalidOperationException(NoMemoryMessage(data.Length));
                }
                throw new InvalidOperationException("Cannot load the database: " + message);
            }
            // Validates the file and the schema before anything is shown.
            database.Query("SELECT count(*) FROM sqlite_master");
            return database;
        }

        private const int SQLITE_NOMEM = 7;

        private static string NoMemoryMessage(int size)
        {
            return string.Format("Not enough memory to open the database ({0:0.0} MB). The program ran out of address space, probably after compressing or loading a very large archive. Save what you have, close and reopen Forza Studio EVO, then open the database again.", size / 1048576.0);
        }

        // The whole database file, as it would be written to disk.
        public byte[] ToBytes()
        {
            long size;
            IntPtr memory = sqlite3_serialize(this.db, Utf8("main"), out size, 0);
            if (memory == IntPtr.Zero)
            {
                throw new InvalidOperationException("Cannot serialize the database.");
            }
            try
            {
                byte[] data = new byte[size];
                Marshal.Copy(memory, data, 0, (int) size);
                return data;
            }
            finally
            {
                sqlite3_free(memory);
            }
        }

        public Result Query(string sql, params object[] args)
        {
            IntPtr statement = this.Prepare(sql, args);
            try
            {
                Result result = new Result();
                int count = sqlite3_column_count(statement);
                result.Columns = new string[count];
                for (int i = 0; i < count; i++)
                {
                    result.Columns[i] = ReadUtf8(sqlite3_column_name(statement, i), -1);
                }
                int rc;
                while ((rc = sqlite3_step(statement)) == SQLITE_ROW)
                {
                    object[] row = new object[count];
                    for (int i = 0; i < count; i++)
                    {
                        switch (sqlite3_column_type(statement, i))
                        {
                            case SQLITE_INTEGER:
                                row[i] = sqlite3_column_int64(statement, i);
                                break;
                            case SQLITE_FLOAT:
                                row[i] = sqlite3_column_double(statement, i);
                                break;
                            case SQLITE_NULL:
                                row[i] = null;
                                break;
                            default:
                                row[i] = ReadUtf8(sqlite3_column_text(statement, i), sqlite3_column_bytes(statement, i));
                                break;
                        }
                    }
                    result.Rows.Add(row);
                }
                if (rc != SQLITE_DONE)
                {
                    throw new InvalidOperationException(this.Error);
                }
                return result;
            }
            finally
            {
                sqlite3_finalize(statement);
            }
        }

        public object Scalar(string sql, params object[] args)
        {
            Result result = this.Query(sql, args);
            return (result.Rows.Count > 0 && result.Columns.Length > 0) ? result.Rows[0][0] : null;
        }

        // Runs a statement and returns the number of changed rows.
        public int Execute(string sql, params object[] args)
        {
            IntPtr statement = this.Prepare(sql, args);
            try
            {
                int rc;
                while ((rc = sqlite3_step(statement)) == SQLITE_ROW)
                {
                }
                if (rc != SQLITE_DONE)
                {
                    throw new InvalidOperationException(this.Error);
                }
                return sqlite3_changes(this.db);
            }
            finally
            {
                sqlite3_finalize(statement);
            }
        }

        // A transaction around a group of writes: Commit keeps them, Rollback puts every one back.
        public void Begin()
        {
            this.Execute("BEGIN");
        }

        public void Commit()
        {
            this.Execute("COMMIT");
        }

        public void Rollback()
        {
            this.Execute("ROLLBACK");
        }

        public long LastInsertRowId
        {
            get { return sqlite3_last_insert_rowid(this.db); }
        }

        private IntPtr Prepare(string sql, object[] args)
        {
            IntPtr statement;
            if (sqlite3_prepare_v2(this.db, Utf8(sql), -1, out statement, IntPtr.Zero) != SQLITE_OK)
            {
                throw new InvalidOperationException(this.Error);
            }
            for (int i = 0; i < args.Length; i++)
            {
                object value = args[i];
                int rc;
                if (value == null || value is DBNull)
                {
                    rc = sqlite3_bind_null(statement, i + 1);
                }
                else if (value is long || value is int || value is short || value is byte || value is bool)
                {
                    rc = sqlite3_bind_int64(statement, i + 1, Convert.ToInt64(value));
                }
                else if (value is double || value is float || value is decimal)
                {
                    rc = sqlite3_bind_double(statement, i + 1, Convert.ToDouble(value));
                }
                else
                {
                    byte[] text = Encoding.UTF8.GetBytes(value.ToString());
                    rc = sqlite3_bind_text(statement, i + 1, text, text.Length, Transient);
                }
                if (rc != SQLITE_OK)
                {
                    sqlite3_finalize(statement);
                    throw new InvalidOperationException(this.Error);
                }
            }
            return statement;
        }

        private string Error
        {
            get
            {
                string message = ReadUtf8(sqlite3_errmsg(this.db), -1);
                if (message != null && message.IndexOf("out of memory", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Not enough memory for the database (out of memory). The program ran out of address space, probably after compressing or loading a very large archive. Save what you have, close and reopen Forza Studio EVO, then try again.";
                }
                return message;
            }
        }

        private static byte[] Utf8(string text)
        {
            return Encoding.UTF8.GetBytes(text + "\0");
        }

        private static string ReadUtf8(IntPtr text, int length)
        {
            if (text == IntPtr.Zero)
            {
                return null;
            }
            if (length < 0)
            {
                length = 0;
                while (Marshal.ReadByte(text, length) != 0)
                {
                    length++;
                }
            }
            byte[] bytes = new byte[length];
            Marshal.Copy(text, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }

        public static string Quote(string identifier)
        {
            return "[" + identifier.Replace("]", "]]") + "]";
        }

        public void Dispose()
        {
            if (this.db != IntPtr.Zero)
            {
                sqlite3_close(this.db);
                this.db = IntPtr.Zero;
            }
        }
    }
}
