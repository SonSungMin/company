using HHI.Data.DMF;
using HHI.ServiceModel;
using HHI.Windows.Forms.DevExtension;
using System.Data;

namespace P72B.Core.BizModule.__RemoveMe___
{
    /// <summary>
    /// 데이터 매퍼를 사용해서 데이터액세스 컴포넌트
    /// </summary>
    internal class MyDbAccessComponent
    {
        public DataSet GetAllMaterial()
        {
            var mapper = new DataMapper(MyBizMDLConfig._EduMapperName);
            return mapper.ExecuteDataSet("GetAllMaterial", null);
        }

        public DataSet GetMaterial(string matNo, string matName)
        {
            var parameter = new QueryParameterCollection();
            parameter.Add("MAT_NO", matNo);
            parameter.Add("MAT_NAME", matName);

            var mapper = new DataMapper(MyBizMDLConfig._EduMapperName);
            return mapper.ExecuteDataSet("GetMaterial", parameter);
        }

        public int SaveMaterial(string matNo, string matName)
        {
            var parameter = new QueryParameterCollection();
            parameter.Add("MAT_NO", matNo);
            parameter.Add("MAT_NAME", matName);

            var mapper = new DataMapper(MyBizMDLConfig._SalesMapperName);
            return mapper.ExecuteNonQuery("SaveMaterial", parameter);
        }

        public int SaveMaterial(QueryParameterCollection parameter)
        {
            var mapper = new DataMapper(MyBizMDLConfig._SalesMapperName);
            return mapper.ExecuteNonQuery("SaveMaterial", parameter);
        }

        public void SaveMaterial(DataTable dt)
        {
            var mapper = new DataMapper(MyBizMDLConfig._SalesMapperName);

            //만건 단위로 처리를 한다.
            var ds = dt.WdxPartitionToTable(10000);

            foreach (DataTable table in ds.Tables)
            {
                var parameter = table.WdxToQueryParameterCollection();

                mapper.ExecuteNonQuery("SaveMaterial", parameter);
            }
        }
    }
}
