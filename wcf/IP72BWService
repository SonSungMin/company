using System;
using System.Collections.Generic;
using System.Data;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace P72BW.SC
{
    [ServiceContract]
    public interface IP72BWService
    {
        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped)]
        DataTableResult ExecuteQuery(string pkg_proc, Dictionary<string, object> parameters);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped)]
        IntResult ExecuteNonQuery(string pkg_proc, Dictionary<string, object> parameters);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped)]
        StringResult ExecuteScalarString(string pkg_proc, Dictionary<string, object> parameters);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped)]
        IntResult ExecuteScalarInt(string pkg_proc, Dictionary<string, object> parameters);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped)]
        DecimalResult ExecuteScalarDecimal(string pkg_proc, Dictionary<string, object> parameters);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped)]
        DataSetResult ExecuteDataSet(string pkg_proc, Dictionary<string, object> parameters);

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped)]
        DataTableResult ExecuteRefCursor(string pkg_proc, Dictionary<string, object> parameters, string refCursorParamName);
    }

    [DataContract]
    public class ServiceResultBase
    {
        [DataMember]
        public bool IsSuccess { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public string ErrorCode { get; set; }

        public ServiceResultBase()
        {
            IsSuccess = true;
            Message = string.Empty;
            ErrorCode = string.Empty;
        }

        public ServiceResultBase(string errorMessage, string errorCode = "")
        {
            IsSuccess = false;
            Message = errorMessage;
            ErrorCode = errorCode;
        }
    }

    [DataContract]
    public class DataTableResult : ServiceResultBase
    {
        [DataMember]
        public DataTable Data { get; set; }

        public DataTableResult() : base() { }

        public DataTableResult(DataTable data) : base()
        {
            Data = data;
        }

        public DataTableResult(string errorMessage, string errorCode = "") : base(errorMessage, errorCode) { }
    }

    [DataContract]
    public class DataSetResult : ServiceResultBase
    {
        [DataMember]
        public DataSet Data { get; set; }

        public DataSetResult() : base() { }

        public DataSetResult(DataSet data) : base()
        {
            Data = data;
        }

        public DataSetResult(string errorMessage, string errorCode = "") : base(errorMessage, errorCode) { }
    }

    [DataContract]
    public class IntResult : ServiceResultBase
    {
        [DataMember]
        public int Data { get; set; }

        public IntResult() : base() { }

        public IntResult(int data) : base()
        {
            Data = data;
        }

        public IntResult(string errorMessage, string errorCode = "") : base(errorMessage, errorCode) { }
    }

    [DataContract]
    public class StringResult : ServiceResultBase
    {
        [DataMember]
        public string Data { get; set; }

        public StringResult() : base() { }

        public StringResult(string data) : base()
        {
            Data = data;
        }

        public StringResult(string errorMessage, string errorCode = "") : base(errorMessage, errorCode) { }
    }

    [DataContract]
    public class DecimalResult : ServiceResultBase
    {
        [DataMember]
        public decimal Data { get; set; }

        public DecimalResult() : base() { }

        public DecimalResult(decimal data) : base()
        {
            Data = data;
        }

        public DecimalResult(string errorMessage, string errorCode = "") : base(errorMessage, errorCode) { }
    }

    [DataContract]
    public class DatabaseParameter
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public object Value { get; set; }

        [DataMember]
        public DbType DbType { get; set; }

        [DataMember]
        public ParameterDirection Direction { get; set; }

        [DataMember]
        public int Size { get; set; }

        public DatabaseParameter()
        {
            Direction = ParameterDirection.Input;
            Size = 0;
        }
    }
}
