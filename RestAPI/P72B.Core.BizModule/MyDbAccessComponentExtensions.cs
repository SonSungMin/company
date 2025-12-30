using HHI.Data.DMF;
using HHI.ServiceModel;
using HHI.Windows.Forms.DevExtension;
using System.Data;

namespace P72B.Core.BizModule.__RemoveMe___.Extensions
{
    /// <summary>
    /// 데이터 매퍼 재사용하는 데이터액세스 컴포넌트 확장 메소드 클래스
    /// </summary>
    internal static class MyDbAccessComponentExtensions
    {
        public static DataSet GetAllMaterial(this DataMapper mapper)
        {
            return mapper.ExecuteDataSet("GetAllMaterial", null);
        }

        public static DataSet GetMaterial(this DataMapper mapper, string matNo, string matName)
        {
            var parameter = new QueryParameterCollection();
            parameter.Add("MAT_NO", matNo);
            parameter.Add("MAT_NAME", matName);

            return mapper.ExecuteDataSet("GetMaterial", parameter);
        }

        public static int SaveMaterial(this DataMapper mapper, string matNo, string matName)
        {
            var parameter = new QueryParameterCollection();
            parameter.Add("MAT_NO", matNo);
            parameter.Add("MAT_NAME", matName);

            return mapper.ExecuteNonQuery("SaveMaterial", parameter);
        }

        public static int SaveMaterial(this DataMapper mapper, QueryParameterCollection parameter)
        {
            return mapper.ExecuteNonQuery("SaveMaterial", parameter);
        }

        public static void SaveMaterial(this DataMapper mapper, DataTable dt)
        {
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
