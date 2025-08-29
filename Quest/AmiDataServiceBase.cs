using Amisys.Framework.Infrastructure.Interfaces;
using Amisys.Framework.Infrastructure.Utility;
using Devart.Common;
using Devart.Data.Oracle;
using Microsoft.Practices.Composite.Events;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Microsoft.Practices.ServiceLocation;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.Objects;
using System.Diagnostics;
using System.Linq;

namespace Amisys.Framework.Infrastructure.DataModels
{
    public class AmiDataServiceBase
    {
        //private OracleMonitor dbMonitor = new OracleMonitor();
        private IEntLibService _entLibService;
        private bool _UseOpDatabase = false;
        private string _QueryFilePath;
        private AmiDbParameterCollection _InParameters;
        private AmiDbParameterCollection _OutParameters;
        private AmiDbParameterCollectionList _UpPrameters;
        private AmiDbParameterCollection _ReplacePrameters;
        public OracleParameterCollection OracleParameters;

        protected IEventAggregator eventAggregator
        {
            get => ServiceLocator.Current.GetInstance<IEventAggregator>();
        }

        public IEntLibService entLibService
        {
            get
            {
                if (this._entLibService == null)
                    this._entLibService = ServiceLocator.Current.GetInstance<IEntLibService>();
                return this._entLibService;
            }
        }
        public bool UseOpDatabase
        {
            get => this._UseOpDatabase;
            set => this._UseOpDatabase = value;
        }

        public string QueryFilePath
        {
            get => this._QueryFilePath;
            set => this._QueryFilePath = value;
        }

        public AmiDbParameterCollection InParameters
        {
            get
            {
                if (this._InParameters == null)
                    this._InParameters = new AmiDbParameterCollection();
                return this._InParameters;
            }
            set => this._InParameters = value;
        }

        public AmiDbParameterCollection OutParameters
        {
            get
            {
                if (this._OutParameters == null)
                    this._OutParameters = new AmiDbParameterCollection();
                return this._OutParameters;
            }
            set => this._OutParameters = value;
        }
        public AmiDbParameterCollectionList UpParameters
        {
            get
            {
                if (this._UpPrameters == null)
                    this._UpPrameters = new AmiDbParameterCollectionList();
                return this._UpPrameters;
            }
            set => this._UpPrameters = value;
        }

        public AmiDbParameterCollection ReplacePrameters
        {
            get
            {
                if (this._ReplacePrameters == null)
                    this._ReplacePrameters = new AmiDbParameterCollection();
                return this._ReplacePrameters;
            }
            set => this._ReplacePrameters = value;
        }

        [Conditional("RELEASE")]
        [Conditional("DEBUG")]
        public void DebugTraceQuery(DbCommand dbCommand) => this.entLibService.DebugTraceQuery(dbCommand);

      [Conditional("RELEASE")]
        [Conditional("DEBUG")]
        public void DebugTraceQueryString(string query) => this.entLibService.DebugTraceLog(query);

        [Conditional("DEBUG")]
        [Conditional("RELEASE")]
        public void DebugTraceQuery<T>(IQueryable<T> query)
        {
            this.entLibService.DebugTraceLog((query as ObjectQuery<T>).ToTraceString());
        }

        //[Conditional("DEBUG")]
        //[Conditional("RELEASE")]
        //public void ActivateOracleMonitor() => ((DbMonitor)this.dbMonitor).IsActive = true;

        protected virtual void InitQueryFilePath() => this._QueryFilePath = "AppQuerySource";

        public virtual string GetQuery(string table)
        {
            string sqlCommand = !string.IsNullOrEmpty(this._QueryFilePath) ? AmiDataAccessHelper.GetQueryWithQueryString(table, this._QueryFilePath) : throw new NotImplementedException("You must to be call InitQueryFilePath");
            if (this._ReplacePrameters != null && this._ReplacePrameters.Count > 0)
                sqlCommand = this.SetReplaceParameters(sqlCommand, this._ReplacePrameters);
            return sqlCommand;
        }

        protected void InitParameters()
        {
            this.InitInOutParameters();
            this.InitReplaceParameters();
            this.InitUpParameters();
        }

        protected void InitInOutParameters()
        {
            this._InParameters = new AmiDbParameterCollection();
            this._OutParameters = new AmiDbParameterCollection();
        }

        protected void InitReplaceParameters() => this._ReplacePrameters = new AmiDbParameterCollection();

        protected void InitUpParameters() => this._UpPrameters = new AmiDbParameterCollectionList();

        protected void AddParameter(string name, DbType dbType, object value)
        {
            this.InParameters.Add(name, dbType, value);
        }

        protected void AddParameter(string name, object value)
        {
            DbType dbType = this.GetDbType(value);
            this.InParameters.Add(name, dbType, value);
        }

        protected void AddOutParameter(string name, DbType dbType, int len)
        {
            this.OutParameters.Add(name, dbType, len);
        }

        protected void AddUpParameter(DbParamType type, string name, DbType dbType)
        {
            this.UpParameters.Add(type, name, dbType);
        }

      protected void AddUpParameter(
          DbParamType type,
          string name,
          DbType dbType,
          string sourceColumn,
          DataRowVersion version)
        {
            this.UpParameters.Add(type, name, dbType, sourceColumn, version);
        }

        protected void AddUpParameter(DbParamType type, string name, DbType dbType, object value)
        {
            this.UpParameters.Add(type, name, dbType, value);
        }

        protected void AddReplaceParameter(string name, object value)
        {
            this.ReplacePrameters.Add(name, value);
        }

        protected void AddReplaceParameter(string name, string xmlPath)
        {
          string str = !string.IsNullOrEmpty(this._QueryFilePath) ? AmiDataAccessHelper.GetQueryWithQueryString(xmlPath, this._QueryFilePath) : throw new NotImplementedException("You must to be call InitQueryFilePath");
            this.ReplacePrameters.Add(name, (object)str);
        }

        protected DbType GetDbType(object obj) => AmiDbTypeConvertor.ToDbType(obj.GetType());

        private string GetDatabaseKey()
        {
            return !this.UseOpDatabase ? ConfigurationManager.AppSettings["AppDefaultDatabase"].ToString() : ConfigurationManager.AppSettings["AppDefaultDatabaseOp"].ToString();
        }

        public string GetDatabaseKey(bool isOpDatabase)
        {
            return !isOpDatabase ? ConfigurationManager.AppSettings["AppDefaultDatabase"].ToString() : ConfigurationManager.AppSettings["AppDefaultDatabaseOp"].ToString();
        }

      public string GetConnectionString()
        {
            return ConfigurationManager.ConnectionStrings[this.GetDatabaseKey()].ToString();
        }

        protected string SetReplaceParameters(string sqlCommand, AmiDbParameterCollection parameters)
        {
            if (parameters != null && parameters.Count > 0)
            {
                foreach (AmiDbParameter amiDbParameter in (IEnumerable<AmiDbParameter>)parameters.OrderByDescending<AmiDbParameter, int>((System.Func<AmiDbParameter, int>)(e => e.ParameterName.Length)))
                {
                    if (amiDbParameter.Value is string)
                    {
                        string newValue = amiDbParameter.Value as string;
                        string oldValue = ":" + amiDbParameter.ParameterName;
                        sqlCommand = sqlCommand.Replace(oldValue, newValue);
                    }
                }
            }
            return sqlCommand;
        }
      public virtual DataSet ExecuteSelect(string query, string tableName = "TABLE")
        {
            try
            {
                Database database = DatabaseFactory.CreateDatabase(this.GetDatabaseKey());
                DbCommand sqlStringCommand = database.GetSqlStringCommand(query);
                this.DebugTraceQueryString($"\n\n{query}\n\n");
                DataSet dataSet = new DataSet();
                database.LoadDataSet(sqlStringCommand, dataSet, tableName);
                return dataSet;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public virtual int ExecuteQuery(string query)
        {
            try
            {
              Database database = DatabaseFactory.CreateDatabase(this.GetDatabaseKey());
                DbCommand sqlStringCommand = database.GetSqlStringCommand(query);
                this.DebugTraceQueryString($"\n\n{query}\n\n");
                return database.ExecuteNonQuery(sqlStringCommand);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public virtual int ExecuteNonQuery(string queryId)
        {
            try
            {
                Database database = DatabaseFactory.CreateDatabase(this.GetDatabaseKey());
                string query = this.GetQuery(queryId);
                DbCommand sqlStringCommand = database.GetSqlStringCommand(query);
                this.AddInParameter(database, sqlStringCommand, this._InParameters);
                this.DebugTraceQuery(sqlStringCommand);
                return database.ExecuteNonQuery(sqlStringCommand);
              }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public virtual int ExecuteStoredProcedure(string storedProcedureName)
        {
            try
            {
                Database database = DatabaseFactory.CreateDatabase(this.GetDatabaseKey());
                DbCommand storedProcCommand = database.GetStoredProcCommand(storedProcedureName);
                this.AddInParameter(database, storedProcCommand, this._InParameters);
                this.DebugTraceQuery(storedProcCommand);
                return database.ExecuteNonQuery(storedProcCommand);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

      public virtual int ExecuteStoredProcedure(
          string storedProcedureName,
          out Dictionary<string, object> outValues)
        {
            try
            {
                outValues = new Dictionary<string, object>();
                Database database = DatabaseFactory.CreateDatabase(this.GetDatabaseKey());
                DbCommand storedProcCommand = database.GetStoredProcCommand(storedProcedureName);
                this.AddInParameter(database, storedProcCommand, this._InParameters);
                this.AddOutParameter(database, storedProcCommand, this._OutParameters);
                this.DebugTraceQuery(storedProcCommand);
                int num = database.ExecuteNonQuery(storedProcCommand);
                outValues = this.RetrieveOutParameters(database, storedProcCommand, this._OutParameters);
                return num;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

      private Dictionary<string, object> RetrieveOutParameters(
          Database db,
          DbCommand dbCommand,
          AmiDbParameterCollection outParams)
        {
            Dictionary<string, object> dictionary = (Dictionary<string, object>)null;
            if (db != null && dbCommand != null && outParams != null)
            {
                dictionary = new Dictionary<string, object>();
                foreach (AmiDbParameter outParam in (List<AmiDbParameter>)outParams)
                {
                    object parameterValue = db.GetParameterValue(dbCommand, outParam.ParameterName);
                    if (!dictionary.ContainsKey(outParam.ParameterName))
                        dictionary.Add(outParam.ParameterName, parameterValue);
                }
            }
            return dictionary;
        }
      public virtual object ExecuteFunction(string functionName, DbType resultType)
        {
            try
            {
                Database database = DatabaseFactory.CreateDatabase(this.GetDatabaseKey());
                DbCommand storedProcCommand = database.GetStoredProcCommand(functionName);
                this.AddInParameter(database, storedProcCommand, this._InParameters);
                database.AddParameter(storedProcCommand, "retval", resultType, 0, ParameterDirection.ReturnValue, true, (byte)0, (byte)0, string.Empty, DataRowVersion.Current, Convert.DBNull);
                database.ExecuteReader(storedProcCommand);
                return database.GetParameterValue(storedProcCommand, "retval");
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
      public virtual DataSet ExecuteDataSet(string queryId)
        {
            try
            {
                Database database = DatabaseFactory.CreateDatabase(this.GetDatabaseKey());
                string query = this.GetQuery(queryId);
                DbCommand sqlStringCommand = database.GetSqlStringCommand(query);
                this.AddInParameter(database, sqlStringCommand, this._InParameters);
                this.DebugTraceQuery(sqlStringCommand);
                return database.ExecuteDataSet(sqlStringCommand);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
      public virtual void LoadDataSet(string queryId, DataSet dataSet, string tableName)
        {
            try
            {
                Database database = DatabaseFactory.CreateDatabase(this.GetDatabaseKey());
                string query = this.GetQuery(queryId);
                DbCommand sqlStringCommand = database.GetSqlStringCommand(query);
                this.AddInParameter(database, sqlStringCommand, this._InParameters);
                this.DebugTraceQuery(sqlStringCommand);
                database.LoadDataSet(sqlStringCommand, dataSet, tableName);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
      public virtual DataSet LoadDataSet(string queryId, string tableName)
        {
            try
            {
                DataSet dataSet = new DataSet();
                this.LoadDataSet(queryId, dataSet, tableName);
                return dataSet;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
      private void AddInParameter(
          Database db,
          DbCommand dbCommand,
          AmiDbParameterCollection amiDbParameterCollection)
        {
            if (amiDbParameterCollection == null || amiDbParameterCollection.Count <= 0)
                return;
            foreach (AmiDbParameter amiDbParameter in (List<AmiDbParameter>)amiDbParameterCollection)
                db.AddInParameter(dbCommand, amiDbParameter.ParameterName, amiDbParameter.DbType, amiDbParameter.Value);
        }

        private void AddOutParameter(
          Database db,
          DbCommand dbCommand,
          AmiDbParameterCollection amiDbParameterCollection)
        {
          if (amiDbParameterCollection == null || amiDbParameterCollection.Count <= 0)
                return;
            foreach (AmiDbParameter amiDbParameter in (List<AmiDbParameter>)amiDbParameterCollection)
                db.AddOutParameter(dbCommand, amiDbParameter.ParameterName, amiDbParameter.DbType, amiDbParameter.Size);
        }

        private void AddUpParameter(
          Database db,
          DbCommand dbCommand,
          AmiDbParameterCollection amiDbParameterCollection)
        {
            if (amiDbParameterCollection == null || amiDbParameterCollection.Count <= 0)
                return;
            foreach (AmiDbParameter amiDbParameter in (List<AmiDbParameter>)amiDbParameterCollection)
            {
                if (amiDbParameter.Value == null)
                {
                  if (string.IsNullOrEmpty(amiDbParameter.SourceColumn))
                        db.AddInParameter(dbCommand, amiDbParameter.ParameterName, amiDbParameter.DbType, amiDbParameter.ParameterName, amiDbParameter.Version);
                    else
                        db.AddInParameter(dbCommand, amiDbParameter.ParameterName, amiDbParameter.DbType, amiDbParameter.SourceColumn, amiDbParameter.Version);
                }
                else
                    db.AddInParameter(dbCommand, amiDbParameter.ParameterName, amiDbParameter.DbType, amiDbParameter.Value);
            }
        }

        public virtual int BulkUpdateDataTableWithQuery(
          List<DataRow> dataSource,
          string upQuery,
          bool isAll = false)
        {
            if (dataSource == null || dataSource.Count <= 0 || string.IsNullOrEmpty(upQuery))
                return 0;
            try
            {string connectionString = this.GetConnectionString();
                int num = 0;
                using (OracleConnection oracleConnection = new OracleConnection(connectionString))
                {
                    if (oracleConnection.State != ConnectionState.Open)
                        ((DbConnection)oracleConnection).Open();
                    if (oracleConnection.State == ConnectionState.Open)
                    {
                        oracleConnection.BeginTransaction();
                        OracleCommand cmd = new OracleCommand(upQuery, oracleConnection);
                        cmd.PassParametersByName = true;
                        this.BindDataTable(dataSource, cmd, DbParamType.Update, isAll);
                        try
                        {
                            this.DebugTraceQueryString("BULK_UPDATE: " + upQuery);
                            cmd.ExecuteArray(dataSource.Count);
                            oracleConnection.Commit();
                          num = dataSource.Count;
                            this.AcceptChanges(dataSource);
                        }
                        catch (Exception ex)
                        {
                            oracleConnection.Rollback();
                            return -1;
                        }
                    }
                }
                return num;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public virtual int BulkUpdateDataTableWithQuery(DataTable dataTable, string upQuery, bool isAll = false)
        {
            if (dataTable == null || string.IsNullOrEmpty(upQuery))
                return 0;
            try
            {
              string connectionString = this.GetConnectionString();
                DataTable dataTable1 = dataTable;
                int num = 0;
                using (OracleConnection oracleConnection = new OracleConnection(connectionString))
                {
                    if (oracleConnection.State != ConnectionState.Open)
                        ((DbConnection)oracleConnection).Open();
                    if (oracleConnection.State == ConnectionState.Open)
                    {
                        oracleConnection.BeginTransaction();
                        try
                        {
                            OracleCommand cmd = new OracleCommand(upQuery, oracleConnection);
                            cmd.PassParametersByName = true;
                            this.BindDataTable(dataTable1, cmd, DbParamType.Update, isAll);
                            this.DebugTraceQueryString("BULK_UPDATE: " + upQuery);
                          cmd.ExecuteArray(dataTable1.Rows.Count);
                            oracleConnection.Commit();
                            num = dataTable1.Rows.Count;
                            this.AcceptChanges(dataTable1);
                        }
                        catch (Exception ex)
                        {
                            oracleConnection.Rollback();
                            throw ex;
                        }
                    }
                }
                return num;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
      public virtual int UpdateDataSetWithDbKey
        (
            DataSet dataSet,
            string tableName,
            string insertQueryId,
            string updateQueryId,
            string deleteQueryId,
            UpdateBehavior updateBehavior = UpdateBehavior.Transactional,
            string dbKey = ""
        )
        {
            try
            {
                if (string.IsNullOrEmpty(dbKey))
                    dbKey = this.GetDatabaseKey();

                Database database = DatabaseFactory.CreateDatabase(dbKey);
                DbCommand insertCommand = null;
                DbCommand updateCommand = null;
                DbCommand deleteCommand = null;
              if (!string.IsNullOrEmpty(insertQueryId))
                {
                    insertCommand = database.GetSqlStringCommand(this.GetQuery(insertQueryId));
                    AmiDbParameterCollection amiDbParameterCollection = this.UpParameters.GetValue(DbParamType.Insert);
                    this.AddUpParameter(database, insertCommand, amiDbParameterCollection);
                }

                if (!string.IsNullOrEmpty(updateQueryId))
                {
                    updateCommand = database.GetSqlStringCommand(this.GetQuery(updateQueryId));
                    AmiDbParameterCollection amiDbParameterCollection = this.UpParameters.GetValue(DbParamType.Update);
                    this.AddUpParameter(database, updateCommand, amiDbParameterCollection);
                }

                if (!string.IsNullOrEmpty(deleteQueryId))
                {
                    deleteCommand = database.GetSqlStringCommand(this.GetQuery(deleteQueryId));
                  AmiDbParameterCollection amiDbParameterCollection = this.UpParameters.GetValue(DbParamType.Delete);
                    this.AddUpParameter(database, deleteCommand, amiDbParameterCollection);
                }

                return database.UpdateDataSet(dataSet, tableName, insertCommand, updateCommand, deleteCommand, updateBehavior);
            }
            catch (System.Data.DBConcurrencyException concurrencyEx)
            {
                throw new InvalidOperationException("데이터를 저장하는 동안 다른 사용자가 해당 정보를 변경하여 충돌이 발생했습니다. 데이터를 새로고침한 후 다시 시도해 주세요.", concurrencyEx);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

      public virtual int UpdateDataSet(
          DataSet dataSet,
          string tableName,
          string insertQueryId,
          string updateQueryId,
          string deleteQueryId,
          UpdateBehavior updateBehavior = UpdateBehavior.Transactional)
        {
            try
            {
                string databaseKey = this.GetDatabaseKey();
                return this.UpdateDataSetWithDbKey(dataSet, tableName, insertQueryId, updateQueryId, deleteQueryId, updateBehavior, databaseKey);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
      public virtual int UpdateDataSet(
          DataTable dataTable,
          string insertQueryId,
          string updateQueryId,
          string deleteQueryId,
          UpdateBehavior updateBehavior = UpdateBehavior.Transactional)
        {
            try
            {
                string databaseKey = this.GetDatabaseKey();
                return this.UpdateDataSetWithDbKey(dataTable.DataSet, dataTable.TableName, insertQueryId, updateQueryId, deleteQueryId, updateBehavior, databaseKey);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public void InitOracleParameters() => this.OracleParameters = new OracleParameterCollection();

      public void AddOracleParameter(
          string colName,
          OracleDbType colType,
          ParameterDirection direction,
          object value)
        {
            if (this.OracleParameters == null)
                this.InitOracleParameters();
            OracleParameter oracleParameter = new OracleParameter(colName, colType);
            ((DbParameter)oracleParameter).Direction = direction;
            ((DbParameter)oracleParameter).Value = value;
            this.OracleParameters.Add(oracleParameter);
        }

        public OracleParameterCollection GetOracleParameters() => this.OracleParameters;

        private void BindDataTable(DataTable tbl, OracleCommand cmd, DbParamType paramType, bool isAll = true)
        {
            AmiDbParameterCollection parameterCollection = this.UpParameters.GetValue(paramType);
          for (int index = 0; index < parameterCollection.Count; ++index)
            {
                AmiDbParameter amiDbParameter = parameterCollection[index];
                OracleDbType oracleType = this.GetOracleType(amiDbParameter.DbType);
                OracleParameter oracleParameter = new OracleParameter(amiDbParameter.ParameterName, oracleType);
                ((DbParameter)oracleParameter).Direction = ParameterDirection.Input;
                if (isAll)
                    ((DbParameter)oracleParameter).Value = this.GetColumnData(tbl, amiDbParameter.ParameterName);
                else
                    ((DbParameter)oracleParameter).Value = this.GetModifiedColumnData(tbl, amiDbParameter.ParameterName, paramType);
                cmd.Parameters.Add(oracleParameter);
            }
        }

        private void BindDataTable(
          List<DataRow> dataSource,
          OracleCommand cmd,
          DbParamType paramType,
          bool isAll = false)
        {
          AmiDbParameterCollection parameterCollection = this.UpParameters.GetValue(paramType);
            for (int index = 0; index < parameterCollection.Count; ++index)
            {
                AmiDbParameter amiDbParameter = parameterCollection[index];
                OracleDbType oracleType = this.GetOracleType(amiDbParameter.DbType);
                OracleParameter oracleParameter = new OracleParameter(amiDbParameter.ParameterName, oracleType);
                ((DbParameter)oracleParameter).Direction = ParameterDirection.Input;
                if (isAll)
                    ((DbParameter)oracleParameter).Value = this.GetColumnData(dataSource, amiDbParameter.ParameterName);
                else
                    ((DbParameter)oracleParameter).Value = this.GetModifiedColumnData(dataSource, amiDbParameter.ParameterName, paramType);
                cmd.Parameters.Add(oracleParameter);
            }
        }

        private object GetColumnData(List<DataRow> dataSource, string p)
        {
          List<object> objectList = new List<object>();
            foreach (DataRow dataRow in dataSource)
                objectList.Add(dataRow[p]);
            return (object)objectList.ToArray();
        }

        private object GetColumnData(DataTable tbl, string p)
        {
            List<object> objectList = new List<object>();
            foreach (DataRow row in (InternalDataCollectionBase)tbl.Rows)
                objectList.Add(row[p]);
            return (object)objectList.ToArray();
        }

        private object GetModifiedColumnData(DataTable tbl, string p, DbParamType paramType)
        {
            List<object> objectList = new List<object>();
            foreach (DataRow row in (InternalDataCollectionBase)tbl.Rows)
            {
                switch (paramType)
                {
                    case DbParamType.Update:
                    if (row.RowState == DataRowState.Modified)
                        {
                            objectList.Add(row[p]);
                            break;
                        }
                        break;
                    case DbParamType.Insert:
                        if (row.RowState == DataRowState.Added)
                        {
                            objectList.Add(row[p]);
                            break;
                        }
                        break;
                    default:
                        if (row.RowState != DataRowState.Unchanged)
                            objectList.Add(row[p]);
                        break;
                }
            }
            return (object)objectList.ToArray();
        }

        private object GetModifiedColumnData(List<DataRow> dataSource, string p, DbParamType paramType)
        {
            List<object> objectList = new List<object>();
            foreach (DataRow dataRow in dataSource)
              {
                switch (paramType)
                {
                    case DbParamType.Update:
                        if (dataRow.RowState == DataRowState.Modified)
                        {
                            objectList.Add(dataRow[p]);
                            break;
                        }
                        break;
                    case DbParamType.Insert:
                        if (dataRow.RowState == DataRowState.Added)
                        {
                            objectList.Add(dataRow[p]);
                            break;
                        }
                        break;
                    default:
                        if (dataRow.RowState != DataRowState.Unchanged)
                            objectList.Add(dataRow[p]);
                        break;
                }
            }
            return (object)objectList.ToArray();
        }

      private OracleDbType GetOracleType(DbType dbType)
        {
            OracleDbType oracleType = (OracleDbType)28;
            switch (dbType)
            {
                case DbType.DateTime:
                    oracleType = (OracleDbType)8;
                    break;
                case DbType.Double:
                    oracleType = (OracleDbType)9;
                    break;
                case DbType.Int32:
                    oracleType = (OracleDbType)11;
                    break;
            }
            return oracleType;
        }

        public virtual int BulkDeleteInsertDataSet(
          DataSet dataSet,
          string tableName,
          string delQuery,
          string insertQueryId,
          bool isAll = true)
          {
            if (dataSet == null || !dataSet.Tables.Contains(tableName))
                return 0;
            try
            {
                string connectionString = this.GetConnectionString();
                DataTable table = dataSet.Tables[tableName];
                int num = 0;
                using (OracleConnection oracleConnection = new OracleConnection(connectionString))
                {
                    if (oracleConnection.State != ConnectionState.Open)
                        ((DbConnection)oracleConnection).Open();
                    if (oracleConnection.State == ConnectionState.Open)
                    {
                        oracleConnection.BeginTransaction();
                        try
                        {
                            if (!string.IsNullOrEmpty(delQuery))
                            {
                              OracleCommand oracleCommand = new OracleCommand(delQuery, oracleConnection);
                                this.DebugTraceQuery((DbCommand)oracleCommand);
                                oracleCommand.ExecuteArray(1);
                            }
                            string query = this.GetQuery(insertQueryId);
                            OracleCommand cmd = new OracleCommand(query, oracleConnection);
                            cmd.PassParametersByName = true;
                            this.BindDataTable(table, cmd, DbParamType.Insert, isAll);
                            this.DebugTraceQueryString("BULK_DEL_INS: " + query);
                            cmd.ExecuteArray(table.Rows.Count);
                            oracleConnection.Commit();
                            num = table.Rows.Count;
                          }
                        catch (Exception ex)
                        {
                            oracleConnection.Rollback();
                            throw ex;
                        }
                    }
                }
                return num;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public virtual int BulkDeleteInsertData(
          OracleParameterCollection _parameters,
          string delQuery,
          string insertQueryId,
          int rowCnt)
          {
            if (_parameters == null)
                return 0;
            try
            {
                string connectionString = this.GetConnectionString();
                int num = 0;
                using (OracleConnection oracleConnection = new OracleConnection(connectionString))
                {
                    if (oracleConnection.State != ConnectionState.Open)
                        ((DbConnection)oracleConnection).Open();
                    if (oracleConnection.State == ConnectionState.Open)
                    {
                        oracleConnection.BeginTransaction();
                        try
                          {
                            if (!string.IsNullOrEmpty(delQuery))
                            {
                                OracleCommand oracleCommand = new OracleCommand(delQuery, oracleConnection);
                                this.DebugTraceQuery((DbCommand)oracleCommand);
                                oracleCommand.ExecuteArray(1);
                            }
                            string query = this.GetQuery(insertQueryId);
                            OracleCommand oracleCommand1 = new OracleCommand(query, oracleConnection);
                            oracleCommand1.PassParametersByName = true;
                            foreach (OracleParameter parameter in (DbParameterCollection)_parameters)
                            {
                              OracleParameter oracleParameter = new OracleParameter(((DbParameter)parameter).ParameterName, parameter.OracleDbType, ((DbParameter)parameter).Value, ((DbParameter)parameter).Direction);
                                oracleCommand1.Parameters.Add(oracleParameter);
                            }
                            this.DebugTraceQueryString("BULK_DEL_INS: " + query);
                            num = oracleCommand1.ExecuteArray(rowCnt);
                            oracleConnection.Commit();
                        }
                        catch (Exception ex)
                          {
                            oracleConnection.Rollback();
                            throw ex;
                        }
                    }
                }
                return num;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public virtual int BulkDeleteInsertDataTable(
          DataTable dt,
          string delQuery,
          string insertQueryId,
          bool isAll = true)
        {
          if (dt == null || dt.Rows.Count <= 0)
                return 0;
            try
            {
                string connectionString = this.GetConnectionString();
                int num = 0;
                using (OracleConnection oracleConnection = new OracleConnection(connectionString))
                {
                    if (oracleConnection.State != ConnectionState.Open)
                        ((DbConnection)oracleConnection).Open();
                    if (oracleConnection.State == ConnectionState.Open)
                    {
                        oracleConnection.BeginTransaction();
                        try
                        {
                          if (!string.IsNullOrEmpty(delQuery))
                            {
                                OracleCommand oracleCommand = new OracleCommand(delQuery, oracleConnection);
                                this.DebugTraceQuery((DbCommand)oracleCommand);
                                oracleCommand.ExecuteArray(1);
                            }
                            string query = this.GetQuery(insertQueryId);
                            OracleCommand cmd = new OracleCommand(query, oracleConnection);
                            cmd.PassParametersByName = true;
                            this.BindDataTable(dt, cmd, DbParamType.Insert, isAll);
                            this.DebugTraceQueryString("BULK_DEL_INS: " + query);
                            cmd.ExecuteArray(dt.Rows.Count);
                            oracleConnection.Commit();
                            num = dt.Rows.Count;
                          }
                        catch (Exception ex)
                        {
                            oracleConnection.Rollback();
                            throw ex;
                        }
                    }
                }
                return num;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

      public virtual int BulkDeleteInsertDataTable(
          List<DataRow> dataSource,
          string delQuery,
          string insertQueryId,
          bool isAll = true)
        {
            if (dataSource == null || dataSource.Count <= 0)
                return 0;
            try
            {
                string connectionString = this.GetConnectionString();
                int num = 0;
                using (OracleConnection oracleConnection = new OracleConnection(connectionString))
                {
                  if (oracleConnection.State != ConnectionState.Open)
                        ((DbConnection)oracleConnection).Open();
                    if (oracleConnection.State == ConnectionState.Open)
                    {
                        oracleConnection.BeginTransaction();
                        try
                        {
                            if (!string.IsNullOrEmpty(delQuery))
                            {
                                OracleCommand oracleCommand = new OracleCommand(delQuery, oracleConnection);
                                this.DebugTraceQuery((DbCommand)oracleCommand);
                                oracleCommand.ExecuteArray(1);
                              }
                            string query = this.GetQuery(insertQueryId);
                            OracleCommand cmd = new OracleCommand(query, oracleConnection);
                            cmd.PassParametersByName = true;
                            this.BindDataTable(dataSource, cmd, DbParamType.Insert, isAll);
                            this.DebugTraceQueryString("BULK_DEL_INS: " + query);
                            cmd.ExecuteArray(dataSource.Count<DataRow>());
                            oracleConnection.Commit();
                            num = dataSource.Count<DataRow>();
                        }
                        catch (Exception ex)
                        {
                            oracleConnection.Rollback();
                            throw ex;
                        }
                      }
                }
                return num;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private void AcceptChanges(List<DataRow> dataSource)
        {
            if (dataSource == null)
                return;
            foreach (DataRow dataRow in dataSource)
                dataRow.AcceptChanges();
        }
      private void AcceptChanges(DataTable dataSource)
        {
            if (dataSource == null)
                return;
            for (int index = 0; index < dataSource.Rows.Count; ++index)
                dataSource.Rows[index].AcceptChanges();
        }

        public virtual int BulkUpdateDataSet(
          DataSet dataSet,
          string tableName,
          string upQueryId,
          bool isAll = false)
        {
            if (dataSet == null || !dataSet.Tables.Contains(tableName) || string.IsNullOrEmpty(upQueryId))
                return 0;
            try
              {
                string connectionString = this.GetConnectionString();
                DataTable table = dataSet.Tables[tableName];
                int num = 0;
                using (OracleConnection oracleConnection = new OracleConnection(connectionString))
                {
                    if (oracleConnection.State != ConnectionState.Open)
                        ((DbConnection)oracleConnection).Open();
                    if (oracleConnection.State == ConnectionState.Open)
                    {
                        oracleConnection.BeginTransaction();
                        try
                        {
                          string query = this.GetQuery(upQueryId);
                            OracleCommand cmd = new OracleCommand(query, oracleConnection);
                            cmd.PassParametersByName = true;
                            this.BindDataTable(table, cmd, DbParamType.Update, isAll);
                            this.DebugTraceQueryString("BULK_UPDATE: " + query);
                            cmd.ExecuteArray(table.Rows.Count);
                            oracleConnection.Commit();
                            num = table.Rows.Count;
                            this.AcceptChanges(table);
                        }
                        catch (Exception ex)
                        {
                            oracleConnection.Rollback();
                            throw ex;
                        }
                      }
                }
                return num;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public virtual int BulkUpdateDataSet(DataTable dataTable, string upQueryId, bool isAll = false)
        {
            if (dataTable == null || string.IsNullOrEmpty(upQueryId))
                return 0;
            try
            {
              string connectionString = this.GetConnectionString();
                DataTable dataTable1 = dataTable;
                int num = 0;
                using (OracleConnection oracleConnection = new OracleConnection(connectionString))
                {
                    if (oracleConnection.State != ConnectionState.Open)
                        ((DbConnection)oracleConnection).Open();
                    if (oracleConnection.State == ConnectionState.Open)
                    {
                        oracleConnection.BeginTransaction();
                        try
                        {
                            string query = this.GetQuery(upQueryId);
                            OracleCommand cmd = new OracleCommand(query, oracleConnection);
                            cmd.PassParametersByName = true;
                          this.BindDataTable(dataTable1, cmd, DbParamType.Update, isAll);
                            this.DebugTraceQueryString("BULK_UPDATE: " + query);
                            cmd.ExecuteArray(dataTable1.Rows.Count);
                            oracleConnection.Commit();
                            num = dataTable1.Rows.Count;
                            this.AcceptChanges(dataTable1);
                        }
                        catch (Exception ex)
                        {
                            oracleConnection.Rollback();
                            throw ex;
                        }
                    }
                }
                return num;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
      public virtual int BulkUpdateDataSet(List<DataRow> dataSource, string upQueryId, bool isAll = false)
        {
            if (dataSource == null || dataSource.Count <= 0 || string.IsNullOrEmpty(upQueryId))
                return 0;
            try
            {
                string connectionString = this.GetConnectionString();
                int num = 0;
                using (OracleConnection oracleConnection = new OracleConnection(connectionString))
                {
                    if (oracleConnection.State != ConnectionState.Open)
                        ((DbConnection)oracleConnection).Open();
                    if (oracleConnection.State == ConnectionState.Open)
                    {
                      oracleConnection.BeginTransaction();
                        string query = this.GetQuery(upQueryId);
                        OracleCommand cmd = new OracleCommand(query, oracleConnection);
                        cmd.PassParametersByName = true;
                        this.BindDataTable(dataSource, cmd, DbParamType.Update, isAll);
                        try
                        {
                            this.DebugTraceQueryString("BULK_UPDATE: " + query);
                            cmd.ExecuteArray(dataSource.Count);
                            oracleConnection.Commit();
                            num = dataSource.Count;
                            this.AcceptChanges(dataSource);
                        }
                        catch (Exception ex)
                        {
                            oracleConnection.Rollback();
                            return -1;
                        }
                      }
                }
                return num;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public virtual DataSet ExecuteDataSetDirect(string query)
        {
            try
            {
                Database database = DatabaseFactory.CreateDatabase(this.GetDatabaseKey());
                DbCommand sqlStringCommand = database.GetSqlStringCommand(query);
                this.DebugTraceQuery(sqlStringCommand);
                return database.ExecuteDataSet(sqlStringCommand);
            }
          catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public virtual int ExecuteNonQueryDirect(string query)
        {
            try
            {
                Database database = DatabaseFactory.CreateDatabase(this.GetDatabaseKey());
                DbCommand sqlStringCommand = database.GetSqlStringCommand(query);
                this.AddInParameter(database, sqlStringCommand, this._InParameters);
                this.DebugTraceQuery(sqlStringCommand);
                return database.ExecuteNonQuery(sqlStringCommand);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
          }
    }
}
