using UnityEngine;
using System.Collections;
using Svelto.Tasks;
using System;

namespace Svelto.Tasks
{
	interface IChainLink<Token>
	{
		Token token { set; }
	}
}





