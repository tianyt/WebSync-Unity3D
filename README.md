WebSync-Unity3D
===============

To get WebSync running in Unity (http://unity3d.com/), you may have to use the WWW (http://docs.unity3d.com/Documentation/ScriptReference/WWW.html) class for HTTP transfers instead of the typical HttpWebRequest class used in typical .NET applications.

Big thanks go to Timothy Graupmann who put together an HttpTransfer class that does just this. To use it, simply instruct WebSync's factory model to use the new WebSyncHttpTransfer class:

HttpTransferFactory.CreateHttpTransfer = () =>
{
  return new WebSyncHttpTransfer();
};