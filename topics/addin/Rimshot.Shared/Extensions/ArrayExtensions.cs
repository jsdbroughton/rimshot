﻿using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Rimshot.Shared.ArrayExtensions {
  internal static class ArrayExtension {

    [MethodImpl( MethodImplOptions.AggressiveInlining )]
    public static T[] ToArray<T> ( this Array arr ) where T : struct {
      T[] result = new T[ arr.Length ];
      Array.Copy( arr, result, result.Length );
      return result;
    }
  }
}
