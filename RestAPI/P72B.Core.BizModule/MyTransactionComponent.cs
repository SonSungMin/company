using HHI.Data.DMF;
using HHI.Transactions;
using P72B.Core.BizModule.__RemoveMe___.Extensions;
using System.Data;

namespace P72B.Core.BizModule.__RemoveMe___
{
    /// <summary>
    /// 분산트랜잭션 기반으로 수행하는 비즈니스 컴포넌트 클래스
    /// DB에 대한 직접적인 코드는 없음.
    /// </summary>
    internal class MyTransactionComponent : BIZBase
    {
        [AutoComplete]
        [Transaction(IsolationLevel = TransactionIsolationLevel.ReadCommitted   // ReadCommitted (기본값) , Serializable => 스냅샷을 지원하는 DBMS(오라클,SQLServer 옵션 적용) 고립화 수준을 올려도 성능(락)에 문제없음
                   , Timeout = 300                                              // 트랜잭션 타임아웃 300초
                                                                                //, TransactionOption = TransactionOption.Required           // 트랜잭션 참여 옵션 (Required 기본값)
                   )]
        public void TranExecute()
        {
            var dac = new MyDbAccessComponent(); //데이터 액세스 객체생성

            var ds = dac.GetAllMaterial(); //데이터를 읽어온다

            dac.SaveMaterial(ds.Tables[0]); //전체 데이터를 저장한다
        }

        public DataSet NoneTranExecute(string matNo, string matName)
        {
            var dac = new MyDbAccessComponent(); //데이터 액세스 객체생성

            return dac.GetMaterial(matNo, matName); //데이터를 읽어온다
        }

        [AutoComplete]
        [Transaction(IsolationLevel = TransactionIsolationLevel.ReadCommitted   // ReadCommitted (기본값) , Serializable => 스냅샷을 지원하는 DBMS(오라클,SQLServer 옵션 적용) 고립화 수준을 올려도 성능(락)에 문제없음
                   , Timeout = 300                                              // 트랜잭션 타임아웃 300초
                                                                                //, TransactionOption = TransactionOption.Required           // 트랜잭션 참여 옵션 (Required 기본값)
                   )]
        public void MultiDbJobTranExecute()
        {
            //과도한 DB Connection을 회피하기 위해서 매퍼 객체를 재사용한다.

            var eduMapper = new DataMapper(MyBizMDLConfig._EduMapperName);
            var salesMapper = new DataMapper(MyBizMDLConfig._SalesMapperName);

            var ds = eduMapper.GetAllMaterial(); //데이터를 읽어온다

            salesMapper.SaveMaterial(ds.Tables[0]); //전체 데이터를 저장한다

            ds = eduMapper.GetMaterial("코드", "품명"); //데이터를 읽어온다

            salesMapper.SaveMaterial(ds.Tables[0]); //전체 데이터를 저장한다
        }
    }
}
