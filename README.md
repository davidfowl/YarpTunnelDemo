# YARP tunnel demo

This demo shows how HttpClient and Kestrel transport extensiblity can be used to implement a tunneling proxy with YARP and ASP.NET Core. The backend project connects to the front end via websockets and manages a set of open connections (which is configurable). The front end project waits for connections from configured backends to configure HTTP client with the appropriate connections to talk to the backend. The net effect is that the backend does not need to have any open ports in order to have traffic routed to it.


![image](https://user-images.githubusercontent.com/95136/166863992-15d09e38-fc27-4af2-a5d5-9b32486cd1ce.png)

