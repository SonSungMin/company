using HHI.ServiceModel;
using System;

namespace P72B.Core.BizModule.__RemoveMe___
{
    /// <summary>
    /// 비즈니스 로직을 서비스로 노출하는 비즈서비스 클래스
    /// </summary>

    //[BizClass()]  // => 클라이언트에서 호출하는 Namespace.Class (Ex) P72B.Core.BizModule.MyBizService
    [BizClass("SalesMDL")]  // => 지정한 이름으로 호출 (Ex) SalesMDL
    public class MyBizService
    {
        //[BizMethod()] // => 클라이언트에서 호출하는 메소드명 (Ex) GetMaterial
        [BizMethod("GetMaterialList")]  // => 지정한 이름으로 호출 (Ex) GetMaterialList
        public BizResponse GetMaterial(BizRequest request)
        {
            var response = new BizResponse();

            try
            {
                var matNo = request.Parameters["MAT_NO"].ToString(); //매개변수
                var matName = request["MAT_NAME"].ToString();        //매개변수(인덱서 접근)

                using (var bizComp = new MyTransactionComponent())
                {
                    response.Result = bizComp.NoneTranExecute(matNo, matName);
                }

                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = $"오류발생\r\n{ex.Message}";
            }

            return response;
        }
    }
}
