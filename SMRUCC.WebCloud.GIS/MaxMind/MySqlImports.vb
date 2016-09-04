﻿Imports System.Runtime.CompilerServices
Imports System.Text
Imports Microsoft.VisualBasic
Imports Microsoft.VisualBasic.DocumentFormat.Csv.DocumentStream.Linq
Imports Microsoft.VisualBasic.DocumentFormat.Csv.Extensions
Imports Microsoft.VisualBasic.Language
Imports Microsoft.VisualBasic.Language.UnixBash
Imports Microsoft.VisualBasic.Linq
Imports Oracle.LinuxCompatibility.MySQL
Imports SMRUCC.WebCloud.GIS.MaxMind.geolite2

Namespace MaxMind

    Public Module MySqlImports

        ''' <summary>
        ''' 
        ''' </summary>
        ''' <param name="mysql"></param>
        ''' <param name="df">GeoLite2-Country-Blocks-IPv4.csv</param>
        ''' <returns></returns>
        ''' 
        <Extension>
        Public Function ImportsGeoLite2CountryBlocksIPv4(mysql As MySQL, df As String) As Boolean
            Dim data As geolite2_country_blocks_ipv4() = df.LoadCsv(Of geolite2_country_blocks_ipv4)
            Dim SQL As New Value(Of String)

            If data.IsNullOrEmpty Then
                Return False
            End If

            Call mysql.Execute(DropTableSQL(Of geolite2_country_blocks_ipv4))
            If Not String.IsNullOrEmpty(SQL = GetCreateTableMetaSQL(Of geolite2_country_blocks_ipv4)()) Then
                Call mysql.Execute(SQL)
            End If

            For Each x As geolite2_country_blocks_ipv4 In data
                Call mysql.ExecInsert(x)
            Next

            Return True
        End Function

        ''' <summary>
        ''' 
        ''' </summary>
        ''' <param name="mysql"></param>
        ''' <param name="df">GeoLite2-Country-Blocks-IPv6.csv</param>
        ''' <returns></returns>
        ''' 
        <Extension>
        Public Function ImportsGeoLite2CountryBlocksIPv6(mysql As MySQL, df As String) As Boolean
            Return mysql.ImportsLargeBlock(Of geolite2_country_blocks_ipv6)(df)
        End Function

        <Extension>
        Public Function ImportsGeoLite2CityBlocksIPv6(mysql As MySQL, df As String) As Boolean
            Return mysql.ImportsLargeBlock(Of geolite2_city_blocks_ipv6)(df)
        End Function

        <Extension>
        Public Function ImportsGeoLite2CityBlocksIPv4(mysql As MySQL, df As String) As Boolean
            Return mysql.ImportsLargeBlock(Of geolite2_city_blocks_ipv4)(df)
        End Function

        <Extension>
        Public Function ImportsLargeBlock(Of T As SQLTable)(mysql As MySQL, df As String) As Boolean
            Dim SQL As New Value(Of String)

            Using reader As New DataStream(df,, 1024 * 1024 * 10)
                Call mysql.Execute(DropTableSQL(Of T))
                If Not String.IsNullOrEmpty(SQL = GetCreateTableMetaSQL(Of T)()) Then
                    Call mysql.Execute(SQL)
                End If

                Call reader.ForEach(Of T)(AddressOf mysql.ExecInsert)
            End Using

            Return True
        End Function

        <Extension>
        Public Function ImportsGeoLite2CountryLocations(mysql As MySQL, DIR As String, Optional locale As String = Nothing) As Boolean
            Dim files As IEnumerable(Of String) =
                If(Not String.IsNullOrEmpty(locale), {
                    $"{DIR}/GeoLite2-Country-Locations-{locale}.csv"
                },
                ls - l - r - wildcards("GeoLite2-Country-Locations*.csv") <= DIR
            )
            Return mysql.ImportsLocationFiles(Of geolite2_country_locations)(files)
        End Function

        <Extension>
        Public Function ImportsGeoLite2CityLocations(mysql As MySQL, DIR As String, Optional locale As String = Nothing) As Boolean
            Dim files As IEnumerable(Of String) =
                If(Not String.IsNullOrEmpty(locale), {
                    $"{DIR}/GeoLite2-City-Locations-{locale}.csv"
                },
                ls - l - r - wildcards("GeoLite2-City-Locations*.csv") <= DIR
            )
            Return mysql.ImportsLocationFiles(Of geolite2_city_locations)(files)
        End Function

        <Extension>
        Public Function ImportsLocationFiles(Of T As SQLTable)(mysql As MySQL, files As IEnumerable(Of String)) As Boolean
            Call mysql.ClearTable(Of T)

            For Each df As String In files
                Dim data = df.LoadCsv(Of T)
                Dim trans As String = String.Join(vbLf, data.ToArray(Function(x) x.GetInsertSQL))
                Call mysql.CommitTransaction(trans)
            Next

            Return True
        End Function

        <Extension>
        Public Function UpdateGeographicalView(mysql As MySQL, Optional locale As String = "en") As String
            Dim indexed As New List(Of Long)
            Dim err As New Value(Of String)

            If Not (err = mysql.ClearTable(Of geographical_information_view)) Is Nothing Then
                Return err
            End If

            Dim geonames As geolite2_city_locations() = mysql.Query(Of geolite2_city_locations)(
                $"SELECT * FROM maxmind_geolite2.geolite2_city_locations WHERE locale_code = '{locale}';"
            )
            Dim geoHash = (From x As geolite2_city_locations
                           In geonames
                           Select x
                           Group x By x.geoname_id Into Group) _
                                .ToDictionary(Function(x) x.geoname_id,
                                              Function(x) x.Group.First)

            Call mysql.ForEach(Of geolite2_city_blocks_ipv4)(
                "SELECT * FROM maxmind_geolite2.geolite2_city_blocks_ipv4;",
                Sub(x)
                    If indexed.IndexOf(x.geoname_id) > -1 Then
                        Return
                    End If
                    If Not geoHash.ContainsKey(x.geoname_id) Then
                        Return
                    End If

                    Dim info As geolite2_city_locations = geoHash(x.geoname_id)
                    Dim view As New geographical_information_view With {
                        .city_name = info.city_name,
                        .country_iso_code = info.country_iso_code,
                        .geoname_id = x.geoname_id,
                        .country_name = info.country_name,
                        .latitude = x.latitude,
                        .longitude = x.longitude,
                        .subdivision_1_name = info.subdivision_1_name,
                        .subdivision_2_name = info.subdivision_2_name
                    }

                    Call mysql.ExecInsert(view)
                End Sub)

            Return Nothing
        End Function
    End Module
End Namespace