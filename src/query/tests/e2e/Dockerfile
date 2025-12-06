FROM docker.io/library/python:3.14.1-slim-bookworm@sha256:5d17fc066275d26bb2ffe05bc89367dc665310200b5f4cfa8b294e97dc679bff
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
