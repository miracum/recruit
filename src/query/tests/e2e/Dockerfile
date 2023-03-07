FROM docker.io/library/python:3.11.2-alpine@sha256:1a5c14626ca7911ae16465aa338ed3f5c1bc0212a3eb16263d0d476bf3d0132d
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
