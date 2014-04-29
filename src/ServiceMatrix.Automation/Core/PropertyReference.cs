﻿namespace NServiceBusStudio.Core
{
    using System;

	/// <summary>
	/// Provides a static factory for properties.
	/// </summary>
	public static class PropertyReference
	{
		/// <summary>
		/// Creates a property with the specified getter and setter delegates.
		/// </summary>
		public static PropertyReference<T> Create<T>(Func<T> getter, Action<T> setter)
		{
			return new PropertyReference<T>(getter, setter);
		}
	}

	/// <summary>
	/// Encapsulates the getter and setter for a property.
	/// </summary>
	public class PropertyReference<T>
	{
		private Func<T> getter;
		private Action<T> setter;

		public PropertyReference(Func<T> getter, Action<T> setter)
		{
			this.getter = getter;
			this.setter = setter;
		}

		/// <summary>
		/// Gets or sets the property value. Catches exceptions in
		/// the getter and returns the <typeparamref name="T"/> default value.
		/// </summary>
		public T Value
		{
			get
			{
				try
				{
					return getter();
				}
				catch (Exception)
				{
					return default(T);
				}
			}
			set { setter(value); }
		}
	}
}
