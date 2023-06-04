FROM docker.io/library/python:3.11.3-alpine@sha256:4e8e9a59bf1b3ca8e030244bc5f801f23e41e37971907371da21191312087a07
WORKDIR /opt/tests

COPY requirements.txt .
RUN pip install --require-hashes --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
