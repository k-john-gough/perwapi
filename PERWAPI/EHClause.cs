using System;
using System.Collections;

namespace QUT.PERWAPI
{
    /**************************************************************************/
    internal enum EHClauseType { Exception, Filter, Finally, Fault = 4 }

    internal class EHClause
    {
        EHClauseType clauseType;
        uint tryOffset, tryLength, handlerOffset, handlerLength, filterOffset = 0;
        MetaDataElement classToken = null;

        internal EHClause(EHClauseType cType, uint tOff, uint tLen, uint hOff, uint hLen)
        {
            clauseType = cType;
            tryOffset = tOff;
            tryLength = tLen;
            handlerOffset = hOff;
            handlerLength = hLen;
        }

        internal void ClassToken(MetaDataElement cToken)
        {
            classToken = cToken;
        }

        internal void FilterOffset(uint fOff)
        {
            filterOffset = fOff;
        }

        internal TryBlock MakeTryBlock(ArrayList labels)
        {
            TryBlock tBlock = new TryBlock(CILInstructions.GetLabel(labels, tryOffset),
                CILInstructions.GetLabel(labels, tryOffset + tryLength));
            CILLabel hStart = CILInstructions.GetLabel(labels, handlerOffset);
            CILLabel hEnd = CILInstructions.GetLabel(labels, handlerOffset + handlerLength);
            HandlerBlock handler = null;
            switch (clauseType)
            {
                case (EHClauseType.Exception):
                    handler = new Catch((Class)classToken, hStart, hEnd);
                    break;
                case (EHClauseType.Filter):
                    handler = new Filter(CILInstructions.GetLabel(labels, filterOffset), hStart, hEnd);
                    break;
                case (EHClauseType.Finally):
                    handler = new Finally(hStart, hEnd);
                    break;
                case (EHClauseType.Fault):
                    handler = new Fault(hStart, hEnd);
                    break;
            }
            tBlock.AddHandler(handler);
            return tBlock;
        }

    }
}
