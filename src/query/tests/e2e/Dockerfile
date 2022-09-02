FROM docker.io/python:3.10-alpine@sha256:3bfac1caa31cc6c0e796b65ba936f320d1e549d80f5ac02c2e4f83a0f04af3aa
WORKDIR /opt/tests

# hadolint ignore=DL3018
RUN apk --no-cache add git

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt
COPY . .

USER 65534:65534
ENTRYPOINT ["pytest"]
CMD [ "test_expected_number_of_resources.py" ]
