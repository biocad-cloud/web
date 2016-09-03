﻿Imports Microsoft.VisualBasic.Net
Imports Oracle.LinuxCompatibility.MySQL
Imports SMRUCC.WebCloud.GIS.MaxMind.geolite2

Namespace MaxMind

    ''' <summary>
    ''' 地理位置服务查询引擎
    ''' </summary>
    Public Class Engine

        Public ReadOnly Property MySQl As MySQL

        Sub New(uri As ConnectionUri)
            Me.MySQl = uri
        End Sub

        Private ReadOnly Mask As String() = {"255.0.0.0", "255.255.0.0", "255.255.255.0"}

        Public Function LocationQuery(IPAddress As String) As FindResult

            If String.IsNullOrEmpty(IPAddress) OrElse Not Net.IPAddress.TryParse(IPAddress, Nothing) Then
                Return FindResult.Null
            End If


            Dim CIDR As String() = (From mask As String
                                In Me.Mask
                                    Select value = New IPv4(IPAddress, mask).CIDR).ToArray
            Dim LQuery = (From s_cidr As String In CIDR
                          Let Query = MySQl.ExecuteScalar(Of geolite2_city_blocks_ipv4)($"SELECT * FROM geoip_services.geolite2_city_blocks_ipv4 where network = '{s_cidr}';")
                          Where Not Query Is Nothing
                          Select Query).FirstOrDefault
            If LQuery Is Nothing Then
                Return FindResult.Null
            End If

            Dim CityLocation = MySQl.ExecuteScalar(Of geolite2_city_locations)($"SELECT * FROM geoip_services.geolite2_city_locations where geoname_id = '{LQuery.geoname_id}';")

            If CityLocation Is Nothing Then
                CityLocation = New geolite2_city_locations
            End If

            Return New FindResult With {
            .CIDR = LQuery.network,
            .city_name = CityLocation.city_name,
            .continent_code = CityLocation.continent_code,
            .continent_name = CityLocation.continent_name,
            .country_iso_code = CityLocation.country_iso_code,
            .country_name = CityLocation.country_name,
            .geoname_id = CityLocation.geoname_id,
            .latitude = LQuery.latitude,
            .longitude = LQuery.longitude,
            .metro_code = CityLocation.metro_code,
            .postal_code = LQuery.postal_code,
            .subdivision_1_iso_code = CityLocation.subdivision_1_iso_code,
            .subdivision_1_name = CityLocation.subdivision_1_name,
            .subdivision_2_iso_code = CityLocation.subdivision_2_iso_code,
            .subdivision_2_name = CityLocation.subdivision_2_name,
            .time_zone = CityLocation.time_zone
        }
        End Function
    End Class
End Namespace