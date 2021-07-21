
# Dynoface
Dynoface is Runtime Dynamic Interface Builder for .NET

### How it works?
We can inspect on a example how to works dynoface.

Firstly i define  a new interface IMyInterface
```csharp
public interface IMyInterface
{
	public void PrintConsole();
}
```
then creating a new Console App.

```csharp
Import System;
Import Dynoface;

public class Program
{
	static void Main(string[] args)
	{
		object target = null;
		/* DynamicInterfaceBuilder uses two parameter.
			First parameter: target object equivalent this
			Second parameter: middle function for execute methods
		*/
		var instance = DynamicInterfaceBuilder.Build<IMyInterface>(target, MiddlewareFunction);
		// Lets run!
		instance.PrintConsole();
	}

	// This is our middlware function for execute IMyInterface methods
	public static object MiddlewareFunction(object instance, Type returnType, object target, MethodInfo methodInfo, object[] args)
    {
        if(methodInfo.Name == "PrintConsole")
        {
            Console.WriteLine("Hello World :)");
        }
        return null;
    }
}
```

All is that :)
