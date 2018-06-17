# NLog.AzureLog
Custom NLog target for Azure Log Analytics

## Example NLog.config
```XML
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      xsi:schemaLocation="http://www.nlog-project.org/schemas/NLog.xsd NLog.xsd"
      autoReload="true"
      throwExceptions="false"
      internalLogLevel="Off" internalLogFile="c:\temp\nlog-internal.log">

  <extensions>
    <add assembly="NLog.AzureLog" />
  </extensions>

  <targets>
    <target xsi:type="Azure" name="Azure" CustomerId="__WORKSPACE-ID__" SharedKey="__PRIMARY-KEY__"  LogName="__NAME__" >
      <layout xsi:type="JsonLayout">
        <attribute name="machine" layout="${machinename}" />
        <attribute name="time" layout="${longdate}" />
        <attribute name="level" layout="${level:upperCase=true}"/>
        <attribute name="message" layout="${message}" />
      </layout>
    </target>
  </targets>

  <rules>
    <!-- add your logging rules here -->

    <logger name="*" minlevel="Debug" writeTo="Azure" />

  </rules>
</nlog>
```
