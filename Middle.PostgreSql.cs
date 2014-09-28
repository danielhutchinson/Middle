using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Dynamic;
using System.Linq;
using Npgsql;

namespace Middle.PostgreSql
{
    public interface IMiddle
    {
        T ExecuteQuerySingle<T>(string querySql, params object[] args) where T : new();
        dynamic ExecuteQuerySingleDynamic(string querySql, params object[] args);
        IEnumerable<T> ExecuteQuery<T>(string querySql, params object[] args) where T : new();
        IEnumerable<dynamic> ExecuteQueryDynamic(string querySql, params object[] args);
        List<int> Transaction(params NpgsqlCommand[] cmds);
    }

    public class MiddlePostgres : IMiddle
    {
        public string ConnectionString { get; set; }

        public MiddlePostgres(string connectionStringName)
        {
            this.ConnectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
        }

        public T ExecuteQuerySingle<T>(string querySql, params object[] args) where T : new()
        {
            return this.ExecuteQuery<T>(querySql, args).FirstOrDefault();
        }

        public dynamic ExecuteQuerySingleDynamic(string querySql, params object[] args)
        {
            return this.ExecuteQueryDynamic(querySql, args).First();
        }

        public IEnumerable<T> ExecuteQuery<T>(string querySql, params object[] args) where T : new()
        {

            using (var reader = OpenReader(querySql, args))
            {
                while (reader.Read())
                {
                    yield return reader.ToSingle<T>();
                }
                reader.Dispose();
            }

        }

        public IEnumerable<dynamic> ExecuteQueryDynamic(string querySql, params object[] args)
        {
            using (var reader = OpenReader(querySql, args))
            {
                while (reader.Read())
                {
                    yield return reader.RecordToExpando(); ;
                }
            }
        }

        public NpgsqlCommand BuildCommand(string querySql, params object[] args)
        {
            var cmd = new NpgsqlCommand(querySql);
            cmd.AddParams(args);
            return cmd;
        }

        private NpgsqlDataReader OpenReader(string querySql, params object[] args)
        {
            var conn = new NpgsqlConnection(this.ConnectionString);
            var cmd = BuildCommand(querySql, args);
            cmd.Connection = conn;

            conn.Open();

            var rdr = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            return rdr;
        }

        public List<int> Transaction(params NpgsqlCommand[] cmds)
        {

            var results = new List<int>();
            using (var conn = new NpgsqlConnection(this.ConnectionString))
            {
                conn.Open();
                using (var transact = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var cmd in cmds)
                        {
                            cmd.Transaction = transact;
                            cmd.Connection = conn;
                            results.Add(cmd.ExecuteNonQuery());
                        }
                        transact.Commit();
                    }
                    catch (NpgsqlException x)
                    {
                        transact.Rollback();
                        throw (x);
                    }
                    finally
                    {
                        conn.Close();
                    }
                }
            }
            return results;
        }
    }

    public static class Extensions
    {
        public static List<T> ToList<T>(this IDataReader rdr) where T : new()
        {
            var result = new List<T>();
            while (rdr.Read())
            {
                result.Add(rdr.ToSingle<T>());
            }
            return result;
        }

        public static T ToSingle<T>(this IDataReader rdr) where T : new()
        {

            var item = new T();
            var props = item.GetType().GetProperties();

            foreach (var prop in props)
            {
                for (int i = 0; i < rdr.FieldCount; i++)
                {
                    if (rdr.GetName(i).Equals(prop.Name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var val = rdr.GetValue(i);
                        if (val != DBNull.Value)
                        {
                            prop.SetValue(item, val);
                        }
                        else
                        {
                            prop.SetValue(item, null);
                        }
                    }
                }

            }

            return item;
        }

        public static void AddParams(this NpgsqlCommand cmd, params object[] args)
        {
            foreach (var item in args)
            {
                AddParam(cmd, item);
            }
        }

        public static void AddParam(this NpgsqlCommand cmd, object item)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = string.Format("@{0}", cmd.Parameters.Count);
            if (item == null)
            {
                p.Value = DBNull.Value;
            }
            else
            {
                if (item is Guid)
                {
                    p.Value = item.ToString();
                    p.DbType = DbType.String;
                    p.Size = 4000;
                }
                else
                {
                    p.Value = item;
                }
                if (item is string)
                    p.Size = ((string)item).Length > 4000 ? -1 : 4000;
            }
            cmd.Parameters.Add(p);
        }

        public static List<dynamic> ToExpandoList(this IDataReader reader)
        {
            var result = new List<dynamic>();
            while (reader.Read())
            {
                result.Add(reader.RecordToExpando());
            }
            return result;
        }

        public static dynamic RecordToExpando(this IDataReader reader)
        {
            dynamic e = new ExpandoObject();
            var d = e as IDictionary<string, object>;
            for (int i = 0; i < reader.FieldCount; i++)
                d.Add(reader.GetName(i), DBNull.Value.Equals(reader[i]) ? null : reader[i]);
            return e;
        }
    }

}
