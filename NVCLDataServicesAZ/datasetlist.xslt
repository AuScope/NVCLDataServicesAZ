<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" >
  <xsl:output method="html"/>

    <xsl:template match="/">
      <html lang="en">
        <head>
          <title>Dataset List</title>
          <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.1/dist/css/bootstrap.min.css" rel="stylesheet" integrity="sha384-4bw+/aepP/YC94hEpVNVgiZdgIC5+VKNBQNGCHeKRQN+PtmoHDEXuppvnDJzQIu9" crossorigin="anonymous"/>
        </head>
        <body >
          <h2>Dataset List</h2>
          
          <table class="table">
            <thead>
              <tr>
                <th scope="col">DatasetName</th>
                <th scope="col">boreholeURI</th>
                <th scope="col">createdDate</th>
                <th scope="col">Download Available</th>
              </tr>
            </thead>
            <tbody>
            <xsl:for-each select="DatasetCollection/Dataset">
                <tr>
                  <th scope="row">
                    <xsl:value-of select="DatasetName" />
                  </th>
                  <td>
                    <xsl:value-of select="boreholeURI" />
                  </td>
                  <td>
                    <xsl:value-of select="createdDate" />
                  </td>
                  <td>
                    <xsl:choose>
                      <xsl:when test="downloadLink !=''">
                        <a class="btn btn-primary" role="button">
                        <xsl:attribute name="href">
                          <xsl:value-of select="downloadLink" />
                        </xsl:attribute>
                        Download
                        </a>
                      </xsl:when>
                      <xsl:otherwise>

                        <button type="button" class="btn btn-primary" data-bs-toggle="modal" data-bs-target="#exampleModal" >
                          <xsl:attribute name="data-bs-dsid">
                            <xsl:value-of select="DatasetID" />
                          </xsl:attribute>
                          <xsl:attribute name="data-bs-dsname">
                            <xsl:value-of select="DatasetName" />
                          </xsl:attribute>
                          Request
                        </button>


                      </xsl:otherwise>
                    </xsl:choose>
                  </td>
                </tr>

              </xsl:for-each>
              </tbody>
              </table>
          
          <!-- Modal -->
          <div class="modal fade" id="exampleModal" tabindex="-1" aria-labelledby="exampleModalLabel" aria-hidden="true">
            <div class="modal-dialog">
              <div class="modal-content">
                <div class="modal-header">
                  <h1 class="modal-title fs-5" id="exampleModalLabel">Request File</h1>
                  <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                </div>
                <div class="modal-body">
                  ...
                </div>
                <div class="modal-footer">
                  <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button>
                  <button type="button" class="btn btn-primary modal-request-btn">Request</button>
                </div>
              </div>
            </div>
          </div>
          <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.1/dist/js/bootstrap.bundle.min.js" integrity="sha384-HwwvtgBNo3bZJJLYd8oVXjrBZt8cqVSpeBNS5n7C8IVInixGAoxmnlMuBnhbgrkm" crossorigin="anonymous"> </script>
          <script >

            var makedlrequest = async function (dsid ) {
            try {
            const response = await fetch('downloadtsg.html?' + new URLSearchParams({datasetid: dsid }));
            //Response from server
            if (!response.ok) {
            throw new Error("request failed");
            }
            }catch(e){
            //handle error
            console.log(e)
            }

            return false;
            };

            const exampleModal = document.getElementById('exampleModal')
            if (exampleModal) {
            exampleModal.addEventListener('show.bs.modal', event => {

            const button = event.relatedTarget

            const dsid = button.getAttribute('data-bs-dsid')
            const dsname = button.getAttribute('data-bs-dsname')

            // Update the modal's content.
            const modalTitle = exampleModal.querySelector('.modal-body')
            const requestbtn = exampleModal.querySelector('.modal-request-btn')

            modalTitle.textContent = `The TSG file for dataset ${dsname} isn't currently available.  You can request it.  The time it will take to generate will depend on how many other requests are in the queue but should be finished within 30 mins.`
            requestbtn.classList.remove('d-none');

            requestbtn.addEventListener("click", function (e) {
            e.preventDefault();
            modalTitle.textContent = `The TSG file for dataset ${dsname} has been requested. It should be available within 30 mins.  Reload this page or return to it later to download the file.`

            requestbtn.classList.add('d-none');
            makedlrequest(dsid);
            });

            })
            }
          </script>
              </body>
      </html>
    </xsl:template>
  
</xsl:stylesheet>
