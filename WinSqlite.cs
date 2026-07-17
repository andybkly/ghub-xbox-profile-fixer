using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GHubProfileUtility
{
    internal static class WinSqlite
    {
        private const string Dll = "winsqlite3.dll";
        private const int Ok = 0, Row = 100, Done = 101, OpenReadOnly = 1, OpenReadWrite = 2;
        private static readonly IntPtr Transient = new IntPtr(-1);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_open_v2(byte[] name, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_close(IntPtr db);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int bytes, out IntPtr stmt, IntPtr tail);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_step(IntPtr stmt);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_blob(IntPtr stmt, int col);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_column_bytes(IntPtr stmt, int col);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern long sqlite3_column_int64(IntPtr stmt, int col);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr sqlite3_column_text(IntPtr stmt, int col);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_bind_blob(IntPtr stmt, int index, byte[] value, int bytes, IntPtr destructor);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] private static extern int sqlite3_bind_int64(IntPtr stmt, int index, long value);

        private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value + "\0");
        private static string Error(IntPtr db) => Marshal.PtrToStringAnsi(sqlite3_errmsg(db));

        private static IntPtr Open(string path, int flags)
        {
            IntPtr db;
            int rc = sqlite3_open_v2(Utf8(path), out db, flags, IntPtr.Zero);
            if (rc != Ok) throw new InvalidOperationException("Could not open database: " + Error(db));
            return db;
        }

        private static IntPtr Prepare(IntPtr db, string sql)
        {
            IntPtr statement;
            int rc = sqlite3_prepare_v2(db, Utf8(sql), -1, out statement, IntPtr.Zero);
            if (rc != Ok) throw new InvalidOperationException("SQLite error: " + Error(db));
            return statement;
        }

        public static byte[] ReadData(string path, out long id)
        {
            IntPtr db = Open(path, OpenReadOnly), statement = IntPtr.Zero;
            try
            {
                statement = Prepare(db, "SELECT _id, FILE FROM DATA ORDER BY _id LIMIT 1");
                if (sqlite3_step(statement) != Row) throw new InvalidOperationException("The DATA table is empty.");
                id = sqlite3_column_int64(statement, 0);
                int size = sqlite3_column_bytes(statement, 1);
                byte[] result = new byte[size];
                Marshal.Copy(sqlite3_column_blob(statement, 1), result, 0, size);
                return result;
            }
            finally
            {
                if (statement != IntPtr.Zero) sqlite3_finalize(statement);
                sqlite3_close(db);
            }
        }

        public static void WriteData(string path, long id, byte[] data)
        {
            IntPtr db = Open(path, OpenReadWrite), statement = IntPtr.Zero;
            try
            {
                Execute(db, "BEGIN IMMEDIATE");
                statement = Prepare(db, "UPDATE DATA SET FILE=? WHERE _id=?");
                if (sqlite3_bind_blob(statement, 1, data, data.Length, Transient) != Ok || sqlite3_bind_int64(statement, 2, id) != Ok)
                    throw new InvalidOperationException("Could not bind converted data: " + Error(db));
                if (sqlite3_step(statement) != Done) throw new InvalidOperationException("Could not update database: " + Error(db));
                sqlite3_finalize(statement); statement = IntPtr.Zero;
                Execute(db, "COMMIT");
            }
            catch
            {
                try { Execute(db, "ROLLBACK"); } catch { }
                throw;
            }
            finally
            {
                if (statement != IntPtr.Zero) sqlite3_finalize(statement);
                sqlite3_close(db);
            }
        }

        private static void Execute(IntPtr db, string sql)
        {
            IntPtr statement = Prepare(db, sql);
            try
            {
                int rc = sqlite3_step(statement);
                if (rc != Done && rc != Row) throw new InvalidOperationException("SQLite error: " + Error(db));
            }
            finally { sqlite3_finalize(statement); }
        }

        public static string IntegrityCheck(string path)
        {
            IntPtr db = Open(path, OpenReadOnly), statement = IntPtr.Zero;
            try
            {
                statement = Prepare(db, "PRAGMA integrity_check");
                if (sqlite3_step(statement) != Row) return "No result";
                return Marshal.PtrToStringAnsi(sqlite3_column_text(statement, 0));
            }
            finally
            {
                if (statement != IntPtr.Zero) sqlite3_finalize(statement);
                sqlite3_close(db);
            }
        }
    }
}
