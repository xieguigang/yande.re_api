Imports System.Xml.Serialization

Namespace Models

    Public Class Pools

    End Class

    <XmlRoot("pool")> Public Class Pool

        <XmlAttribute> Public Property id As String
        <XmlAttribute> Public Property name As String
        <XmlAttribute> Public Property created_at As String
        <XmlAttribute> Public Property updated_at As String
        <XmlAttribute> Public Property user_id As String
        <XmlAttribute> Public Property is_public As String
        <XmlAttribute> Public Property post_count As String
        <XmlAttribute> Public Property description As String

        <XmlElement("description")>
        Public Property description1 As String
        <XmlArray("posts")>
        Public Property posts As post()

    End Class
End Namespace