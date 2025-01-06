using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BuzzGUI.Common.Settings
{
	[AttributeUsage(AttributeTargets.Property)]
	public class Default : Attribute
	{
		public Default(object value) { Value = value; }
		public object Value { get; private set; }
	}
}
