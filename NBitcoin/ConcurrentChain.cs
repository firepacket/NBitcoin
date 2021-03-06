﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBitcoin
{
	public class ConcurrentChain : ChainBase
	{
		ConcurrentDictionary<uint256, ChainedBlock> _BlocksById = new ConcurrentDictionary<uint256, ChainedBlock>();
		ConcurrentDictionary<int, ChainedBlock> _BlocksByHeight = new ConcurrentDictionary<int, ChainedBlock>();
		object @lock = new object();

		/// <summary>
		/// Force a new tip for the chain
		/// </summary>
		/// <param name="pindex"></param>
		/// <returns>forking point</returns>
		public override ChainedBlock SetTip(ChainedBlock block)
		{
			lock(@lock)
			{
				int height = Tip == null ? -1 : Tip.Height;
				foreach(var orphaned in EnumerateThisToFork(block))
				{
					ChainedBlock unused;
					_BlocksById.TryRemove(orphaned.HashBlock, out unused);
					_BlocksByHeight.TryRemove(orphaned.Height, out unused);
					height--;
				}
				var fork = GetBlock(height);
				foreach(var newBlock in block.EnumerateToGenesis()
					.TakeWhile(c => c != Tip))
				{
					_BlocksById.AddOrUpdate(newBlock.HashBlock, newBlock, (a, b) => newBlock);
					_BlocksByHeight.AddOrUpdate(newBlock.Height, newBlock, (a, b) => newBlock);
				}
				_Tip = block;
				return fork;
			}
			
		}

		private IEnumerable<ChainedBlock> EnumerateThisToFork(ChainedBlock block)
		{
			if(_Tip == null)
				yield break;
			var tip = _Tip;
			while(true)
			{
				if(tip.Height > block.Height)
				{
					yield return tip;
					tip = tip.Previous;
				}
				else if(tip.Height < block.Height)
				{
					block = block.Previous;
				}
				else if(tip.Height == block.Height)
				{
					if(tip.HashBlock == block.HashBlock)
						break;
					yield return tip;
					block = block.Previous;
					tip = tip.Previous;
				}
			}
		}

		#region IChain Members

		public override ChainedBlock GetBlock(uint256 id)
		{
			ChainedBlock result;
			_BlocksById.TryGetValue(id, out result);
			return result;
		}

		public override ChainedBlock GetBlock(int height)
		{
			ChainedBlock result;
			_BlocksByHeight.TryGetValue(height, out result);
			return result;
		}

		volatile ChainedBlock _Tip;
		public override ChainedBlock Tip
		{
			get
			{
				return _Tip;
			}
		}

		public override int Height
		{
			get
			{
				return Tip.Height;
			}
		}

		#endregion

		protected override IEnumerable<ChainedBlock> EnumerateFromStart()
		{
			int i = 0;
			while(true)
			{
				var block = GetBlock(i);
				if(block == null)
					yield break;
				yield return block;
				i++;
			}
		}
	}
}
