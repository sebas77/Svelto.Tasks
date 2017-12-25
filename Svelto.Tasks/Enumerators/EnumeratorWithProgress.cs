#if TO_IMPLEMENT_PROPERLY
using System.Collections;

namespace Svelto.Tasks
{
	public class EnumeratorWithProgress: IEnumerator
	{
		public 	float progress { get { return _progressFunction();} }
		public object Current { get { return _enumerator.Current; } }
		
		public EnumeratorWithProgress(IEnumerator enumerator, System.Func<float> progressFunction)
		{
			_enumerator = enumerator;
			_progressFunction = progressFunction;
		}
		
		virtual public bool MoveNext()
		{
			return _enumerator.MoveNext();
		}
		public void Reset()
		{
			_enumerator.Reset();
		}
		
		System.Func<float>  _progressFunction;
		IEnumerator         _enumerator;
	}
}

#endif