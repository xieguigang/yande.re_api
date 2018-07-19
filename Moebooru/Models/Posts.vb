Imports System.Xml.Serialization
Imports Microsoft.VisualBasic.ApplicationServices.Debugging

Namespace Models

    Public Class Posts

        <XmlAttribute> Public Property count As Integer
        <XmlAttribute> Public Property offset As Integer

        <XmlElement("post")>
        Public Property posts As post()

    End Class

    Public Class post : Implements IVisualStudioPreviews

        Public ReadOnly Property Previews As String Implements IVisualStudioPreviews.Previews
            Get
                Return (<img src=<%= sample_url %> style="width:100%; height:100%;"/>).ToString
            End Get
        End Property

        Public Property sample_url As String

    End Class
End Namespace